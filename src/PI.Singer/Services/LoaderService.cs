using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Adapters;
using AutoMapper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PI.Shared;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Interfaces;
using PI.Shared.Services;

namespace Services;

public class LoaderService
{
    private readonly Dictionary<string, WorkItem> _workItems = new();

    private readonly IEntityIdentityAdapter _identityAdapter;
    private readonly IIntegrationAppointmentAdapter _integrationAppointmentAdapter;
    private readonly ILeadTypeAdapter _leadTypeAdapter;
    private readonly ILeadAdapter _leadAdapter;
    private readonly LeadBuilderService _leadBuilderService;
    private readonly ILogger<LoaderService> _logger;
    private readonly IMapper _mapper;
    private readonly ObjectTypeService _objectTypeService;
    private readonly MongoConnection _connection;
    private readonly IOrganizationAdapter _organizationAdapter;
    private readonly IUserAdapter _userAdapter;
    private readonly ISingerConfigAdapter _configAdapter;

    public LoaderService(
        ILogger<LoaderService> logger,
        IMapper mapper,
        ObjectTypeService objectTypeService,
        MongoConnection connection,
        IEntityIdentityAdapter identityAdapter,
        IIntegrationAppointmentAdapter integrationAppointmentAdapter,
        ILeadTypeAdapter leadTypeAdapter,
        ILeadAdapter leadAdapter,
        LeadBuilderService leadBuilderService,
        IOrganizationAdapter organizationAdapter,
        IUserAdapter userAdapter,
        ISingerConfigAdapter configAdapter
    )
    {
        _identityAdapter = identityAdapter;
        _integrationAppointmentAdapter = integrationAppointmentAdapter;
        _leadTypeAdapter = leadTypeAdapter;
        _leadAdapter = leadAdapter;
        _leadBuilderService = leadBuilderService;
        _logger = logger;
        _mapper = mapper;
        _objectTypeService = objectTypeService;
        _connection = connection;
        _organizationAdapter = organizationAdapter;
        _userAdapter = userAdapter;
        _configAdapter = configAdapter;
    }

    public async Task<WorkItem> InitWorkItemAsync(Guid configId, string tag)
    {
        using var scope = _logger.AddScope(new
        {
            ConfigId = configId,
            Tag = tag
        });

        _logger.LogInformation("Initializing Load");

        var workItem = new WorkItem
        {
            Config = await _configAdapter.GetByIdAsync(configId),
            Import = await _configAdapter.MarkLoadStartAsync(configId, tag),
        };

        var map = new Dictionary<string, SingerStreamConfig>();
        foreach (var stream in workItem.Config.Streams)
        {
            switch (stream.Value)
            {
                case LeadStreamConfig lead:
                    map.Add(stream.Key, await ResolveAsync(lead));
                    break;

                case AppointmentStreamConfig appointment:
                    map.Add(stream.Key, await ResolveAsync(appointment));
                    break;
            }
        }

        workItem.CachedStreams = map;

        AddWorkItem(configId, tag, workItem);

        return workItem;
    }

    private async Task<SingerStreamConfig> ResolveAsync(AppointmentStreamConfig config)
    {
        var map = _mapper.Map<CachedAppointmentStreamConfig>(config);
        var leadType = await _leadTypeAdapter.GetByIdAsync(config.LeadTypeId);
        map.LeadType = leadType;

        return map;
    }

    private async Task<CachedLeadStreamConfig> ResolveAsync(LeadStreamConfig config)
    {
        var map = _mapper.Map<CachedLeadStreamConfig>(config);
        var leadType = await _leadTypeAdapter.GetByIdAsync(config.LeadTypeId);
        var entity = await _identityAdapter.GetEntityByIdAsync(leadType.EntityId);
        map.LeadType = leadType;
        map.Context = entity.Context;

        return map;
    }

    public void RemoveWorkItem(Guid configId, string tag)
    {
        var key = configId + tag;
        _workItems.Remove(key);
    }

    public WorkItem GetWorkItem(Guid configId, string tag)
    {
        var key = configId + tag;
        return _workItems.TryGetValue(key, out var workItem) ? workItem : null;
    }

    private void AddWorkItem(Guid configId, string tag, WorkItem workItem)
    {
        var key = configId + tag;
        _workItems.Add(key, workItem);
    }

    public async Task<bool> UpdateStateAsync(WorkItem workItem, JObject json)
    {
        var state = json.GetValue("value").ToObject<SingerState>();
        workItem.Import.State = state;

        await _configAdapter.UpdateAsync(workItem.Import.Id, state);
        return true;
    }

    public async Task<bool> LoadRecordAsync(WorkItem workItem, JObject json, string route)
    {
        var stream = json.GetValue("stream").Value<string>();

        var context = new
        {
            Stream = stream,
            Route = route,
            ConfigId = workItem.Config.Id,
            workItem.Import.Tag
        };

        using var scope = _logger.AddScope(context);
        var streamConfig = workItem[stream];
        if (streamConfig == null)
        {
            _logger.LogError("No configuration for {Stream}", stream);
            return false;
        }

        var record = json.GetValue("record") as JObject;

        try
        {
            var result = streamConfig switch
            {
                CachedLeadStreamConfig lead => await LoadLeadAsync(workItem.Import, stream, lead, record),
                CachedAppointmentStreamConfig appt => await LoadAppointmentAsync(workItem.Import, stream, appt, record),
                OrganizationStreamConfig org => await LoadOrganizationAsync(workItem.Import, stream, org, record),
                UserStreamConfig user => await LoadUserAsync(workItem.Import, stream, user, record),
                OrganizationMembershipStreamConfig membership => await LoadOrgMembershipAsync(workItem.Import, stream, membership, record),
                _ => (IResult)Result<string>.Error("Unexpected object")
            };

            if (!result.IsSuccess)
            {
                _logger.LogInformation("Failed to load from {Stream}: {Status}", streamConfig.Name, result.Status);
                return true;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load from {Stream}", streamConfig.Name);
            return false;
        }
    }

    public async Task<bool> ExtractEndAsync(Guid configId, string tag)
    {
        using var scope = _logger.AddScope(new
        {
            ConfigId = configId,
            Tag = tag
        });

        var workItem = GetWorkItem(configId, tag);
        await _configAdapter.MarkLoadCompleteAsync(workItem.Import.Id, workItem.Import.State);

        RemoveWorkItem(configId, tag);

        _logger.LogInformation("Completed Load");

        return true;
    }

    public async Task<Result<SingerLoadingLog>> LoadOrgMembershipAsync(SingerJob import, string stream, OrganizationMembershipStreamConfig membership, JObject record)
    {
        var log = new SingerLoadingLog
        {
            ConfigId = import.ConfigId,
            JobId = import.Id,
            Stream = stream
        };

        try
        {
            var context = CreateContext(import, membership, record);
            context.Parsed = FieldMapBuilder.Auto(context.Body).Values;

            await LoadOrgMembership2Async(context, log);
            return log.IsSuccessful ? Result.Success(log) : Result<SingerLoadingLog>.Error(log.Outcome);
        }
        catch (Exception ex)
        {
            log.Result = SingerLoadResult.Exception;
            log.Outcome = ex.Message;
            return Result<SingerLoadingLog>.Error(log.Outcome);
        }
        finally
        {
            await _configAdapter.LogAsync(log);
        }
    }

    private async Task LoadOrgMembership2Async(LoaderContext<OrganizationMembershipStreamConfig> context, SingerLoadingLog log)
    {
        var map = context.Parsed;
        if (!map.TryGetValue(context.Config.OrganiztionExternalIdField, out var organizationIdObj) ||
            !map.TryGetValue(context.Config.UserExternalIdField, out var userIdObj) ||
            organizationIdObj == null || userIdObj == null
           )
        {
            log.Result = SingerLoadResult.Failed;
            log.Outcome = "Missing required fields";
            return;
        }

        log.AddReference($"{context.Config.OrganiztionExternalIdField}:{organizationIdObj}");
        log.AddReference($"{context.Config.UserExternalIdField}:{userIdObj}");

        var (userEntity, userIdentity) = await _identityAdapter.FindAsync(context.ActorContext, context.Config.ExternalProvider.ToString(), userIdObj.ToString());
        if (!(userEntity is User user))
        {
            log.Result = SingerLoadResult.Failed;
            log.Outcome = $"User not found: {userIdObj}";
            return;
        }

        log.AddReference(user.Id);
        
        var (organizationEntity, orgIdentity) = await _identityAdapter.FindAsync(context.ActorContext, context.Config.ExternalProvider.ToString(), organizationIdObj.ToString());
        if (!(organizationEntity is Organization organization))
        {
            log.Result = SingerLoadResult.Failed;
            log.Outcome = "Organization not found";
            return;
        }

        log.AddReference(organization.Id);

        if (organization.AccountId != context.AccountId)
        {
            log.Result = SingerLoadResult.Failed;
            log.Outcome = "Account mismatch for organization";
            return;
        }

        if (user.AccountId != context.AccountId)
        {
            log.Result = SingerLoadResult.Failed;
            log.Outcome = $"Account mismatch for user: {user.Id}";
            return;
        }

        if (user.OrganizationId.HasValue && user.OrganizationId.Value != organization.Id)
        {
            if (!context.Config.AllowReassignment)
            {
                log.Result = SingerLoadResult.Failed;
                log.Outcome = $"Moving user to another org ({organization.Id}) not supported: {user.Id}";
                return;
            }
            // log?
            // ...
        }

        var result = await _userAdapter.SetOrganization(context.ActorContext, user, organization.Id);
        if (result == null)
        {
            log.Result = SingerLoadResult.Failed;
            log.Outcome = $"Failed to Update User: {user.Id}";
            return;
        }

        log.Result = SingerLoadResult.Added;
        log.Outcome = $"{user.Name} assigned to {organization.Name}";
    }

    public async Task<Result<SingerLoadingLog>> LoadUserAsync(SingerJob import, string stream, UserStreamConfig user, JObject record)
    {
        var log = new SingerLoadingLog
        {
            ConfigId = import.ConfigId,
            JobId = import.Id,
            Stream = stream
        };

        try
        {
            var context = CreateContext(import, user, record);
            context.Parsed = FieldMapBuilder.Auto(context.Body).Values;

            if (!context.Parsed.TryGetValue(context.Config.ExternalIdField, out var id))
            {
                return Result<SingerLoadingLog>.Error($"Missing required {context.Config.ExternalIdField} field");
            }

            log.AddReference(id);

            await LoadUserAsync2(context, log);
            return log.IsSuccessful ? Result.Success(log) : Result<SingerLoadingLog>.Error(log.Outcome);
        }
        catch (Exception ex)
        {
            log.Result = SingerLoadResult.Exception;
            log.Outcome = ex.Message;
            return Result<SingerLoadingLog>.Error(log.Outcome);
        }
        finally
        {
            await _configAdapter.LogAsync(log);
        }
    }

    private async Task LoadUserAsync2(LoaderContext<UserStreamConfig> context, SingerLoadingLog log)
    {
        var map = context.Parsed;
        if (!map.TryGetValue(context.Config.ExternalIdField, out var id))
        {
            log.Outcome = $"Missing required {context.Config.ExternalIdField} field";
            log.Result = SingerLoadResult.Failed;
            return;
        }

        var isActive = !AnyTrue(context.Config.InactiveConditions,map);

        if (!map.TryGetValue(context.Config.UserNameField, out var name))
        {
            name = null;
        }

        // TODO: can infer anything about user level
        // ...

        var isMainIdentity = string.Equals(context.Config.ExternalIdField, context.Config.UpdateIdentityField);

        // exists? 
        var (mainEntity, mainIdentity) = await _identityAdapter.FindAsync(context.ActorContext, context.Config.ExternalProvider, id.ToString());
        if (mainIdentity != null)
        {
            log.AddReference(mainEntity.Id);

            if (mainEntity is not User existingUser)
            {
                log.Outcome = "Identity not associated with an user";
                log.Result = SingerLoadResult.Failed;
                return;
            }

            if (mainEntity.AccountId != context.AccountId)
            {
                log.Outcome = "Can't update user, wrong account";
                log.Result = SingerLoadResult.Failed;
                return;
            }

            if (mainEntity.IsActive != isActive)
            {
                if (!await _userAdapter.UpdateIsActiveAsync(context.ActorContext, mainEntity.Id, isActive))
                {
                    _logger.LogError("Failed to update isActive for {UserId}", mainEntity.Id);
                }
                else
                {
                    _logger.LogInformation("{UserId} changed IsActive to {IsActive}", mainEntity.Id, isActive);
                }
            }

            // update identity data
            if (isMainIdentity)
            {
                if (!(await _identityAdapter.UpdateDataAsync(context.ActorContext, mainEntity.Id, mainIdentity, map)))
                {
                    _logger.LogError("Failed to update {IdentityId} for {UserId}", mainIdentity.Id, mainEntity.Id);
                }
            }

            // other identities
            await AddAdditionalIdentities(context, log, existingUser, map);

            log.Result = SingerLoadResult.Updated;
            log.Outcome = "Updated";
            return;
        }

        var entityId = Guid.NewGuid();
        var identityId = Guid.NewGuid();

        var user = await _userAdapter.CreateAsync(
            context.ActorContext,
            new User
            {
                Id = entityId,
                AccountId = context.AccountId,
                EntityId = context.AccountId,
                UserRoleId = EntityRoleId.User.ToString(),
                Name = name?.ToString(),
                MainIdentityId = identityId,
                IsActive = isActive,
            }, new EntityIdentity
            {
                Id = identityId,
                IdentityProviderId = context.Config.ExternalProvider.ToString(),
                ExternalId = id.ToString(),
                Name = name?.ToString(),
                ExternalIdentity = null,
                Data = isMainIdentity ? map : null,
            });

        if (user == null)
        {
            log.Result = SingerLoadResult.Failed;
            log.Outcome = "Failed to create user";
            return;
        }

        log.AddReference(user.Id);

        await AddAdditionalIdentities(context, log, user, map);

        log.AddReference(user.Id);
        log.Result = SingerLoadResult.Added;
        log.Outcome = "Added";
    }

    private async Task AddAdditionalIdentities(LoaderContext<UserStreamConfig> context, SingerLoadingLog log, User user, Dictionary<string, object> map)
    {
        if (context.Config.AdditionalExternalIdFields == null) return;

        foreach (var field in context.Config.AdditionalExternalIdFields)
        {
            if (!map.TryGetValue(field.Key, out var externalId)) continue;

            log.AddReference($"{field.Key}:{externalId}");

            bool isMainIdentity = Equals(field, context.Config.UpdateIdentityField);

            var (entity, identity) = await _identityAdapter.FindAsync(context.ActorContext, context.Config.ExternalProvider, externalId.ToString());
            if (entity != null)
            {
                if (user.Id != entity.Id)
                {
                    // bad...bad...bad
                    _logger.LogError("Identity mismatch {UserId} vs {IdentityId} for {ExternalId}", user.Id, identity.Id, externalId);
                    continue;
                }

                if (isMainIdentity)
                {
                    if (!(await _identityAdapter.UpdateDataAsync(context.ActorContext, entity.Id, identity, map)))
                    {
                        _logger.LogError("Failed to update {Identity} for {User}", identity.Id, entity.Id);
                    }
                }

                continue;
            }

            var name = field.Value ?? $"{context.Config.Name}.{field.Key}";

            // add identity
            await _identityAdapter.AddAsync(
                context.ActorContext,
                user.Id,
                new EntityIdentity
                {
                    Id = Guid.NewGuid(),
                    IdentityProviderId = context.Config.ExternalProvider.ToString(),
                    ExternalId = externalId.ToString(),
                    Name = name,
                    ExternalIdentity = null,
                    Data = isMainIdentity ? map : null,
                }
            );
        }
    }

    public async Task<Result<SingerLoadingLog>> LoadOrganizationAsync(SingerJob import, string stream, OrganizationStreamConfig org, JObject record)
    {
        var log = new SingerLoadingLog
        {
            ConfigId = import.ConfigId,
            JobId = import.Id,
            Stream = stream
        };

        try
        {
            var context = CreateContext(import, org, record);
            context.Parsed = FieldMapBuilder.Auto(context.Body).Values;

            if (!context.Parsed.TryGetValue(context.Config.ExternalIdField, out var id))
            {
                return Result<SingerLoadingLog>.Error($"Missing field {context.Config.ExternalIdField}");
            }

            log.AddReference(id);

            await LoadOrganizationAsync2(context, log);
            return log.IsSuccessful ? Result.Success(log) : Result<SingerLoadingLog>.Error(log.Outcome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception loading Organization");
            log.Result = SingerLoadResult.Exception;
            log.Outcome = ex.Message;
            return Result<SingerLoadingLog>.Error(log.Outcome);
        }
        finally
        {
            await _configAdapter.LogAsync(log);
        }
    }

    private async Task LoadOrganizationAsync2(LoaderContext<OrganizationStreamConfig> context, SingerLoadingLog log)
    {
        var map = context.Parsed;

        if (!map.TryGetValue(context.Config.ExternalIdField, out var externalId))
        {
            log.Result = SingerLoadResult.Failed;
            return;
        }

        var isActive = !AnyTrue(context.Config.InactiveConditions, map);
        if (!map.TryGetValue(context.Config.NameField, out var name))
        {
            log.Result = SingerLoadResult.Failed;
            log.Outcome = $"Missing field {context.Config.NameField}";
            return;
        }

        // exists? 
        var (entity, identity) = await _identityAdapter.FindAsync(context.ActorContext, context.Config.ExternalProvider, externalId.ToString());
        if (entity != null)
        {
            log.AddReference(entity.Id);

            if (entity is not Organization organization)
            {
                log.Result = SingerLoadResult.Failed;
                log.Outcome = "Identity is not for an organization";
                return;
            }

            if (entity.AccountId != context.AccountId)
            {
                log.Result = SingerLoadResult.Failed;
                log.Outcome = "Can't update organization in different account";
                return;
            }

            if (entity.IsActive != isActive)
            {
                if (!await _organizationAdapter.UpdatePropertyAsync(context.ActorContext, entity.Id, x => x.IsActive, isActive))
                {
                    _logger.LogError("Failed to update isActive for {OrganizationId}", entity.Id);
                }
                else
                {
                    _logger.LogInformation("{Organization} changed IsActive to {IsActive}", entity.Id, isActive);
                }
            }

            if (!string.Equals(entity.Name, name.ToString()))
            {
                if (!await _organizationAdapter.UpdatePropertyAsync(context.ActorContext, entity.Id, x => x.Name, name.ToString()))
                {
                    _logger.LogError("Failed to update Name for {OrganizationId}", entity.Id);
                }
                else
                {
                    _logger.LogInformation("{OrganizationId} changed Name to {Name}", entity.Id, name);
                }
            }

            _logger.LogInformation("Update Data: {JSON}", JsonConvert.SerializeObject(map));
            if (!await _identityAdapter.UpdateDataAsync(context.ActorContext, entity.Id, identity, map))
            {
                _logger.LogError("Failed to update {IdentityId} for {OrganizationId}", identity.Id, entity.Id);
            }

            log.Result = SingerLoadResult.Updated;
            log.Outcome = "Updated";
            return;
        }

        var entityId = Guid.NewGuid();
        var identityId = Guid.NewGuid();
        var org = await _organizationAdapter.CreateAsync(
            context.ActorContext,
            new Organization
            {
                Id = entityId,
                AccountId = context.AccountId,
                EntityId = context.AccountId,
                Name = name.ToString(),
                IsActive = isActive,
                FlowId = FlowIds.Billing, // TODO: to get from the object type 
            }, new EntityIdentity
            {
                Id = identityId,
                IdentityProviderId = context.Config.ExternalProvider.ToString(),
                ExternalId = externalId.ToString(),
                Name = name.ToString(),
                ExternalIdentity = null,
                Data = map
            }
        );

        log.AddReference(org.Id);

        log.Result = SingerLoadResult.Added;
        log.Outcome = "Added";
    }

    private async Task<Result<SingerLoadingLog>> LoadAppointmentAsync(SingerJob import, string stream, CachedAppointmentStreamConfig appt, JObject record)
    {
        var log = new SingerLoadingLog
        {
            ConfigId = import.ConfigId,
            JobId = import.Id,
            Stream = stream
        };

        try
        {
            var context = CreateContext(import, appt, record);
            log.Message = JsonConvert.SerializeObject(context.Body.RemoveNulls(), Formatting.None, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            context.Parsed = FieldMapBuilder.Auto(context.Body).Values;
            if (!context.Parsed.TryGetValue(context.Config.ExternalIdField, out var id) || id == null)
            {
                log.Result = SingerLoadResult.Failed;
                log.Outcome = $"Missing Id: {context.Config.ExternalIdField}";
                return Result<SingerLoadingLog>.Error(log.Outcome);
            }

            await LoadAppointmentAsync2(context, log);

            return log.IsSuccessful ? Result.Success(log) : Result<SingerLoadingLog>.Error(log.Outcome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception loading Appointment");

            log.Result = SingerLoadResult.Exception;
            log.Outcome = ex.Message;
            return Result<SingerLoadingLog>.Error(log.Outcome);
        }
        finally
        {
            await _configAdapter.LogAsync(log);
        }
    }

    private async Task LoadAppointmentAsync2(LoaderContext<CachedAppointmentStreamConfig> context, SingerLoadingLog log)
    {
        var map = context.Parsed;

        if (!map.TryGetValue(context.Config.ExternalIdField, out var externalId) || externalId == null)
        {
            log.Result = SingerLoadResult.Failed;
            log.Outcome = $"Missing Id: {context.Config.ExternalIdField}";
            return;
        }

        log.AddReference(externalId);

        var isActive = !AnyTrue(context.Config.InactiveConditions,map);
        var (existingAppt, existing) = await _integrationAppointmentAdapter.FindAsync(context.Config.IntegrationId, externalId.ToString());

        log.AddReference(existingAppt?.Id);

        if (!map.TryGetValue(context.Config.LeadExternalIdField, out var leadExternalId) || leadExternalId == null)
        {
            if (existing == null)
            {
                if (isActive)
                {
                    log.Result = SingerLoadResult.Failed;
                    log.Outcome = $"Couldn't find Lead field: {context.Config.LeadExternalIdField}";
                }
                else
                {
                    log.Result = SingerLoadResult.Skip;
                    log.Outcome = $"Couldn't find Lead field for inactive appt: {context.Config.LeadExternalIdField}";
                }

                return;
            }

            leadExternalId = null;
        }
        else
        {
            log.AddReference($"{context.Config.LeadExternalIdField}:{leadExternalId}");
        }

        var user = default(User);
        var entity = default(IEntity);
        var organizationId = default(Guid?);
        foreach (var field in context.Config.EntityExternalIdFields)
        {
            if (!map.TryGetValue(field, out var entityExternalId) || entityExternalId == null) continue;

            log.AddReference($"{field}:{entityExternalId}");

            var (entityFound, _) = await _identityAdapter.FindAsync(context.ActorContext, context.Config.ExternalProvider.ToString(), entityExternalId.ToString());
            if (entityFound == null)
            {
                _logger.LogError("Couldn't find Identity for {Field}: {ExternalId}", field, entityExternalId);
                continue;
            }

            entity = entityFound;

            if (entityFound is User userFound)
            {
                if (user != null && user.Id != userFound.Id)
                {
                    _logger.LogCritical("mismatch or users");
                    log.Result = SingerLoadResult.Failed;
                    log.Outcome = "Users mismatch";
                    return;
                }

                user = userFound;
            }

            var orgIdFound = entityFound switch
            {
                User _userFound => _userFound.OrganizationId,
                Organization organization => organization.Id,
                _ => default
            };

            if (orgIdFound.HasValue)
            {
                if (organizationId.HasValue && organizationId.Value != orgIdFound.Value)
                {
                    _logger.LogCritical("mismatch of organizations");
                    log.Result = SingerLoadResult.Failed;
                    log.Outcome = "Organizations mismatch";
                    return;
                }

                organizationId = orgIdFound;
            }
        }

        IContextWithActor entityContext;
        if (user != null)
        {
            if (!organizationId.HasValue)
            {
                _logger.LogInformation("Incomplete {UserId}", user.Id);
                entityContext = new IncompleteUserContext(user.Id, user.AccountId)
                    .With(context.ActorContext.Actor);
            }
            else
            {
                entityContext = (user.OrganizationId.HasValue ? user.Context : UserContext.OrgUser(user.Id, user.Name, EntityRoleId.User, organizationId.Value, user.AccountId)
                    ).With(context.ActorContext.Actor);
            }
        }
        else
        {
            entityContext = entity?.Context.With(context.ActorContext.Actor) ?? context.ActorContext;
        }

        object startDateObj = null;
        object endDateObj = null;
        if (!map.TryGetValue(context.Config.StartField, out startDateObj) ||
            !map.TryGetValue(context.Config.EndField, out endDateObj) ||
            !TryParseDate(startDateObj, out var startDate) ||
            !TryParseDate(endDateObj, out var endDate))
        {
            if (existing == null)
            {
                log.Outcome = $"Failed to parse dates '{startDateObj}' and/or '{endDateObj}'";
                log.Result = SingerLoadResult.Skip;
                return;
            }

            // trying to unset or missing date, ignore
            startDate = existingAppt.Start;
            endDate = existingAppt.End;
        }

        var lead = default(Lead);
        if (leadExternalId == null)
        {
            lead = await _leadAdapter.GetByIdAsync(context.ActorContext, existingAppt.LeadId);
            log.AddReference(lead.Id);
        }
        else
        {
            (lead, _) = await _leadAdapter.GetFirstByIntegrationAsync(context.ActorContext, context.Config.IntegrationId, leadExternalId.ToString());
        }

        if (lead == null)
        {
            if (existing != null)
            {
                log.Outcome = $"Trying to change to different lead: {leadExternalId}";
                log.Result = SingerLoadResult.Failed;
                return;
            }

            lead = await _connection.CreateAsync(
                entityContext,
                new Lead
                {
                    Id = Guid.NewGuid(),
                    AccountId = context.AccountId,
                    Name = $"Appointment:{externalId}",
                    EntityId = organizationId ?? context.AccountId,
                    CreatedOn = DateTime.UtcNow,
                    LeadTypeId = context.Config.LeadType.Id,

                    // using the FlowId in the LeadType object is wrong, for now just as a fallback to the previous behavior
                    FlowId = context.Config.LeadType.InitialFlowId ?? context.Config.LeadType.FlowId,
                    ObjectStatusId = context.Config.LeadType.InitialObjectStatusId ?? LeadStatusIds.Initial,
                    Integrations = new[]
                    {
                        new LeadIntegration
                        {
                            IntegrationId = context.Config.IntegrationId,
                            ExternalId = leadExternalId.ToString(),
                            Tag = context.Config.LeadExternalIdField // ???
                        }
                    }
                }
            );

            if (lead == null)
            {
                log.Outcome = "Couldn't create lead";
                log.Result = SingerLoadResult.Failed;
                return;
            }

            await _objectTypeService.FireCreateEventAsync(entityContext, lead, evt =>
            {
                evt.Description = "Lead implicitly created by Appointment";
                evt.Action ??= "ObjectCreated";
            });
        }

        log.AddReference(lead.Id);

        var timeZoneId = user?.TimeZoneId;
        if (string.IsNullOrWhiteSpace(timeZoneId) && organizationId.HasValue)
        {
            var organization = await _connection.Filter<Entity>()
                .Eq(x => x.Id, organizationId)
                .IncludeField(x => x.TimeZoneId)
                .FirstOrDefaultAsync();

            timeZoneId = organization?.TimeZoneId;
        }

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId,
            EntityId = entityContext.EntityId.Value,
            OrganizationId = lead.EntityId,
            LeadId = lead.Id,
            Parent = new ReferencedObject
            {
                ObjectId = lead.Id,
                ObjectType = nameof(Lead),
            },
            CreatorId = null, // ????
            AppointmentTypeId = context.Config.AppointmentTypeId,
            Start = startDate,
            End = endDate,
            Refs = new Dictionary<string, object>
            {
                // { "sf_WorkOrderLineItem", "" },
                { "sf_ServiceAppointment", externalId },
            },
            // CancelledOn = !isActive ? DateTime.UtcNow : (DateTime?)null,
            // Subject
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                appointment.CalculateMetaData(timeZoneId);
            }

            appointment.Tags = new[] { "Singer", "Salesforce" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't find {TimeZoneId} for {OrganizationId}", timeZoneId, organizationId);
        }

        if (!isActive)
        {
            // since we don't have the date the appt was cancelled 
            // or transitioned to inactive/deleted
            // we assume the lastmodifiedon date (if available)
            var lastModifiedOn = default(DateTime?);
            if (!string.IsNullOrEmpty(context.Config.LastModifiedOnField) && map.TryGetValue(context.Config.LastModifiedOnField, out var lastModified))
            {
                if (TryParseDate(lastModified, out var lastModifiedDate))
                {
                    lastModifiedOn = lastModifiedDate;

                    _logger.LogInformation("Setting CancelledOn for appt {externalId} using {lastModifiedDate}", externalId, lastModifiedDate);
                }
            }

            appointment.CancelledOn = lastModifiedOn.GetValueOrDefault(DateTime.UtcNow);
        }

        if (!string.IsNullOrEmpty(context.Config.CreatedOnField) && map.TryGetValue(context.Config.CreatedOnField, out var createdOn))
        {
            if (TryParseDate(createdOn, out var createdOnDate))
            {
                appointment.CreatedOn = createdOnDate;
            }
        }

        if (!string.IsNullOrEmpty(context.Config.CreatedByExternalIdField) &&
            map.TryGetValue(context.Config.CreatedByExternalIdField, out var createdByExternalId) &&
            createdByExternalId != null
           )
        {
            log.AddReference($"{context.Config.CreatedByExternalIdField}:{createdByExternalId}");
            var (createdBy, _) = await _identityAdapter.FindAsync(context.ActorContext, context.Config.ExternalProvider.ToString(), createdByExternalId.ToString());
            appointment.CreatedBy = createdBy?.Id;
            appointment.CreatorId = createdBy?.Id;
            log.AddReference(createdBy?.Id);

            if (createdBy?.GroupMembership?.Length > 0)
            {
                var group = await _connection.Filter<EntityGroup>()
                    .Eq(x => x.AccountId, createdBy.AccountId)
                    .In(x => x.Id, createdBy.GroupMembership)
                    .FirstOrDefaultAsync();

                appointment.Tool = group?.Name ?? "Other";
            }
        }

        if (map.TryGetValue(context.Config.UrlField, out var url))
        {
            appointment.WebLink = url.ToString();
        }

        // add integration
        var iAppt = new AppointmentIntegration
        {
            AppointmentId = appointment.Id,
            IntegrationId = context.Config.IntegrationId,
            ExternalId = externalId.ToString(),
            Data = map,
            Url = !string.IsNullOrEmpty(appointment.WebLink)
                ? appointment.WebLink
                : (
                    !string.IsNullOrEmpty(context.Config.UrlPrefix) ? $"{context.Config.UrlPrefix}{externalId}" : null
                ),
        };

        if (existing != null)
        {
            appointment.Id = existing.AppointmentId;
            var updateResult = await UpdateAsync(entityContext, appointment, iAppt);
            if (updateResult.IsError)
            {
                log.Result = SingerLoadResult.Failed;
                log.Outcome = updateResult.Status;
            }
            else
            {
                log.Result = SingerLoadResult.Updated;
                log.Outcome = updateResult.IsSuccess ? $"Appointment Update: {string.Join(", ", updateResult.Value.Values)}" : "Nothing to update";
            }

            return;
        }

        appointment.Integrations = new[]
        {
            iAppt
        };

        appointment = await AddAsync(entityContext, appointment);
        if (appointment == null)
        {
            log.Outcome = "Failed to create record";
            log.Result = SingerLoadResult.Failed;
            return;
        }

        log.Result = SingerLoadResult.Added;
        log.Outcome = $"Appointment Created: {appointment.Id}";
        log.AddReference(appointment.Id);

        // fire both create object events
        await _objectTypeService.FireCreateEventAsync(entityContext, appointment, evt =>
        {
            evt.Description = "Appointment Imported";
            evt.Action ??= "ObjectCreated";

            evt.AddRefValue(nameof(Lead), appointment.LeadId);
            evt.AddRefValue(nameof(AppointmentType), appointment.AppointmentTypeId);

            evt.SetMetaValue(nameof(User), user?.Name);
            evt.SetMetaValue(nameof(Lead), lead?.Name);
            // if (organization != null) evt.SetMetaValue(nameof(Organization), organization.Name);
            // evt.SetMetaValue(nameof(AppointmentType), appointmentType.Name);

            evt.SetMetaValue(nameof(Appointment.LocalDate), appointment.LocalDate);
            evt.SetMetaValue(nameof(Appointment.LocalTime), appointment.LocalTime);
            evt.SetMetaValue("TimeZone", timeZoneId);

            evt.SetRefValue(nameof(Integration), iAppt.IntegrationId);
            evt.SetMetaValue(nameof(Integration), IntegrationIds.GetName(iAppt.IntegrationId));
        });

        if (appointment.IsActive && !appointment.CancelledOn.HasValue && appointment.Start > DateTime.UtcNow && lead.NextAppointmentId != appointment.Id)
        {
            // Update lead
            if (lead.NextAppointmentId.HasValue)
            {
                // TODO: should cancel it 
                // ...
            }

            var now = DateTime.UtcNow;

            var firstConversion = !lead.ConvertedOn.HasValue;
            var updateLead = _connection.Filter<Lead>()
                .Eq(x => x.AccountId, lead.AccountId)
                .Eq(x => x.Id, lead.Id)
                .Update
                .Set(x => x.NextAppointmentId, appointment.Id)
                .Set(x => x.AssignedEntityId, appointment.EntityId)
                .Set(x => x.LastModifiedOn, now)
                .Set(x => x.LastActor, entityContext.Actor());

            var modifiedFields = new Dictionary<string, object>
            {
                { nameof(Lead.NextAppointmentId), appointment.Id },
                { nameof(Lead.AssignedEntityId), appointment.EntityId },
            };

            if (firstConversion)
            {
                updateLead.Set(x => x.ConvertedOn, now);

                modifiedFields.Add(nameof(Lead.ConvertedOn), now);
            }

            lead = await updateLead.UpdateAndGetOneAsync();
            if (lead != null)
            {
                await _objectTypeService.FireObjectUpdatedAsync(entityContext, lead, modifiedFields, evt =>
                {
                    evt.Description = "Next Appointment set implicitly";
                    evt.AddRefValue(appointment);
                });
            }
        }
    }

    private async Task<Appointment> AddAsync(IEntityContext context, Appointment appt)
    {
        var entityIds = context.GetEntityIds().ToArray();
        if (entityIds.Length < 1)
        {
            _logger.LogError("Invalid Context {role}", context.Role);
            return null;
        }

        appt.EntityIds = entityIds;
        appt.LastModifiedOn = DateTime.UtcNow;
        appt.LastActor = context.Actor();

        await _connection.InsertAsync(appt);

        return appt;
    }

    private async Task<Result<Dictionary<string, object>>> UpdateAsync(IEntityContext context, Appointment appointment, IIntegrationAppointment integration)
    {
        // TODO: limit by account?
        // ... 
        var existing = await _connection.Filter<Appointment>()
            .Eq(x => x.Id, appointment.Id)
            .FirstOrDefaultAsync();

        if (existing == null) return Result<Dictionary<string, object>>.Error("Appointment doesn't exist");

        var modifiedFields = new Dictionary<string, object>();

        var query = _connection.Filter<Appointment>()
            .Eq(x => x.Id, existing.Id)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor());

        if (existing.Start != appointment.Start || existing.End != appointment.End)
        {
            // TODO: this is an issue for other integrations
            // have to, at the very least, fire events
            // until now we didn't allow changing time of existing appt
            // ... 
            query.Set(x => x.Start, appointment.Start)
                .Set(x => x.End, appointment.End)
                .Set(x => x.IsAllDay, appointment.IsAllDay)
                // have to also figure out what to do with the local time
                // ...
                .Set(x => x.LocalDate, appointment.LocalDate)
                .Set(x => x.LocalTime, appointment.LocalTime)
                .Set(x => x.TimeZoneId, appointment.TimeZoneId)
                ;

            modifiedFields.Add(nameof(Appointment.Start), appointment.Start);
            modifiedFields.Add(nameof(Appointment.End), appointment.End);
            modifiedFields.Add(nameof(Appointment.IsAllDay), appointment.IsAllDay);
            modifiedFields.Add(nameof(Appointment.LocalDate), appointment.LocalDate);
            modifiedFields.Add(nameof(Appointment.LocalTime), appointment.LocalTime);
            modifiedFields.Add(nameof(Appointment.TimeZoneId), appointment.TimeZoneId);
        }

        if (appointment.EntityId != existing.EntityId)
        {
            // TODO: check that is allowed? 
            // ...
            query
                .Set(x => x.EntityId, appointment.EntityId)
                .Set(x => x.EntityIds, context.GetEntityIds())
                ;

            modifiedFields.Add(nameof(Appointment.EntityId), appointment.EntityId);
        }

        if (appointment.LeadId != existing.LeadId)
        {
            // ...
            _logger.LogInformation("Trying to change {Appointment} from {Lead} to {ToLead}, ignore", appointment.Id, existing.LeadId, appointment.LeadId);

            modifiedFields.Add(nameof(Appointment.LeadId), appointment.LeadId);
        }

        if (appointment.AppointmentTypeId != existing.AppointmentTypeId)
        {
            // ...
            _logger.LogInformation("Trying to change {Appointment} from {AppointmentType} to {ToAppointmentType}, ignore", appointment.Id, existing.AppointmentTypeId, appointment.AppointmentTypeId);

            modifiedFields.Add(nameof(Appointment.AppointmentTypeId), appointment.AppointmentTypeId);
        }

        // only update the created by if it is missing
        if (appointment.CreatedBy.HasValue && !existing.CreatedBy.HasValue)
        {
            query.Set(x => x.CreatedBy, appointment.CreatedBy);

            modifiedFields.Add(nameof(Appointment.CreatedBy), appointment.CreatedBy);
        }

        if (appointment.CancelledOn.HasValue && !existing.CancelledOn.HasValue)
        {
            // TODO: appointment was cancelled, fire event
            // ... 
            query
                .Set(x => x.CancelledOn, appointment.CancelledOn)
                .Set(x => x.IsActive, false);

            modifiedFields.Add(nameof(Appointment.CancelledOn), appointment.CancelledBy);
            modifiedFields.Add(nameof(Appointment.IsActive), appointment.IsActive);
        }

        if (appointment.Data != null)
        {
            // ...
        }

        var dict = (existing.Integrations != null) ? existing.Integrations.ToDictionary(x => $"{x.IntegrationId}:{x.ExternalId}") : new Dictionary<string, AppointmentIntegration>();

        var key = $"{integration.IntegrationId}:{integration.ExternalId}";
        if (dict.TryGetValue(key, out var dst))
        {
            // update
            if (integration.Data != null)
            {
                // TODO: merge?
                // ...
                dst.Data = integration.Data;
            }

            if (!string.IsNullOrEmpty(integration.Url)) dst.Url = integration.Url;
            if (!string.IsNullOrEmpty(integration.Status)) dst.Status = integration.Status;
        }
        else
        {
            // add
            var dao = _connection.Map<AppointmentIntegration>(integration);
            dict.Add(key, dao);
        }

        modifiedFields.Add($"{integration.IntegrationId}:{integration.ExternalId}", "*");

        query.Set(x => x.Integrations, dict.Values.ToArray());

        var updatedAppointment = await query.UpdateAndGetOneAsync();
        if (updatedAppointment == null)
        {
            return Result.Error<Dictionary<string, object>>("Failed to update");
        }

        await _objectTypeService.FireObjectUpdatedAsync(context, updatedAppointment, modifiedFields, x =>
        {
            x.Description = "Appointment loaded";

            x.AddRefValue(nameof(Appointment), updatedAppointment);
            x.SetMetaValue(nameof(Appointment.LocalDate), updatedAppointment.LocalDate);
            x.SetMetaValue(nameof(Appointment.LocalTime), updatedAppointment.LocalTime);
            x.SetRefValue(nameof(Integration), integration.IntegrationId);
            x.SetMetaValue(nameof(Integration), IntegrationIds.GetName(integration.IntegrationId));
        });

        return Result.Success(modifiedFields);
    }

    private bool TryParseDate(object dateObject, out DateTime date)
    {
        if (dateObject is DateTime startDate)
        {
            date = startDate;
            return true;
        }

        if (dateObject is string str && DateTime.TryParse(str, out date))
        {
            return true;
        }

        _logger.LogError($"Can't parse date: '{dateObject}'");
        date = default;

        return false;
    }

    private async Task<Result<SingerLoadingLog>> LoadLeadAsync(SingerJob import, string stream, CachedLeadStreamConfig lead, JObject record)
    {
        var log = new SingerLoadingLog
        {
            ConfigId = import.ConfigId,
            JobId = import.Id,
            Stream = stream
        };

        try
        {
            // handle deleted
            // ...

            var context = CreateContext(import, lead, record);
            log.Message = JsonConvert.SerializeObject(context.Body.RemoveNulls(), Formatting.None, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            await LoadLeadAsync(context, log);

            return log.IsSuccessful ? Result.Success(log, log.Outcome) : Result<SingerLoadingLog>.Error(log.Outcome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception loading Lead");

            log.Result = SingerLoadResult.Exception;
            log.Outcome = ex.Message;
            return Result<SingerLoadingLog>.Error(log.Outcome);
        }
        finally
        {
            await _configAdapter.LogAsync(log);
        }
    }

    private async Task LoadLeadAsync(LoaderContext<CachedLeadStreamConfig> context, SingerLoadingLog log)
    {
        var builder = await _leadBuilderService.AddAsync(context.Config.Context.WithActorFrom(context.ActorContext), context.Config.LeadType, log.Message, fireEvents: true);

        if (builder.Failed)
        {
            log.Result = SingerLoadResult.Failed;
            log.Outcome = builder.Error;
            return;
        }

        log.AddReference(builder.Result.Id);
        log.AddReferences(builder.MergedLeadIds?.Select(x => x.ToString()));
        log.AddReferences(builder.IntegrationLeads?.Select(x => $"{x.Tag}:{x.ExternalId}"));

        if (builder.UpdatedFields != null)
        {
            log.Result = builder.MergedLeadIds?.Length > 0 ? SingerLoadResult.Merged : SingerLoadResult.Updated;
            log.Outcome = builder.UpdatedFields != null ? string.Join(", ", builder.UpdatedFields) : null;
        }
        else
        {
            log.Result = SingerLoadResult.Added;
        }
    }

    private class IncompleteUserContext : IEntityContext
    {
        public EntityRoleId Role => EntityRoleId.User;

        public Guid? UserId { get; }

        public Guid? OrganizationId => null;

        public Guid? AccountId { get; }

        public Guid? EntityId => UserId;

        public Guid? ProfileId => null;
        public Guid[] AllProfileIds => [];
        public string ClientId => null;

        public IEnumerable<Guid> GetEntityIds()
        {
            yield return UserId.Value;
            yield return AccountId.Value;
        }

        public IReadOnlyDictionary<string, string[]> Claims => null;

        public IncompleteUserContext(Guid userId, Guid accountId)
        {
            UserId = userId;
            AccountId = accountId;
        }
    }

    private static LoaderContext<T1> CreateContext<T1>(SingerJob job, T1 config, JObject body = null) where T1 : SingerStreamConfig
    {
        return new LoaderContext<T1>(job, config, body);
    }

    private class LoaderContext<T> where T : SingerStreamConfig
    {
        public SingerJob Job { get; }
        public T Config { get; }
        public JObject Body { get; }
        public Dictionary<string, object> Parsed { get; set; }

        public IContextWithActor ActorContext { get; }
        public Guid AccountId => Job.AccountId;

        public LoaderContext(SingerJob job, T config, JObject body)
        {
            Job = job;
            Config = config;
            Body = body;

            Actor.Current = new SingerSyncActor(job.Id);
            ActorContext = Actor.Current.WithContext(new AccountContext(job.AccountId));
        }
    }

    /// <summary>
    /// Evaluate if any condition is true
    /// MISSING FIELDS EVALUATE TO NULL
    /// </summary>
    private static bool AnyTrue(Condition[] conditions, IDictionary<string, object> values)
    {
        if (conditions == null || conditions.Length < 1) return false;
        foreach (var condition in conditions)
        {
            if (condition.Evaluate(values))
            {
                return true;
            }
        }

        return false;
    }
}