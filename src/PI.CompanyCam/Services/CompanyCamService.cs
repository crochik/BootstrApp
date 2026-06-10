using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.CompanyCam.Models;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.SingleUseTickets;
using PI.Shared.Requests;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using Token = PI.CompanyCam.Models.Token;

namespace PI.CompanyCam.Services;

public class CompanyCamService
{
    private const string IntegrationObjectTypeName = $"companycam.{nameof(CCIntegrationConfiguration)}";

    private readonly CCConfiguration _configuration;
    private const string AuthorizeEndpoint = "https://app.companycam.com/oauth/authorize?";
    private const string TokenEndpoint = "https://app.companycam.com/oauth/token";

    private readonly ILogger<CompanyCamService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MongoConnection _connection;
    private readonly DataProtectionService _dataProtectionService;
    private readonly ObjectTypeService _objectTypeService;
    private readonly string _baseUrl;

    private HttpClient Client => _httpClientFactory.CreateClient(nameof(CompanyCamService));

    public CompanyCamService(
        ILogger<CompanyCamService> logger,
        IHttpClientFactory httpClientFactory,
        MongoConnection connection,
        DataProtectionService dataProtectionService,
        ObjectTypeService objectTypeService,
        IConfiguration configuration
    )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _connection = connection;
        _dataProtectionService = dataProtectionService;
        _objectTypeService = objectTypeService;
        _configuration = configuration.GetSection("CompanyCam").Get<CCConfiguration>();
        _baseUrl = configuration.GetValue<string>("BaseUrl");
    }

    public async Task<string> StartLoginAsync(IEntityContext context)
    {
        var ticket = await _connection.InsertAsync(new SingleUseTicket
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId.Value,
            EntityId = context.GetOwnerEntityId(),
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            Name = "Add CompanyCam integration",
            ExpiresOn = DateTime.UtcNow.AddMinutes(10),
            IsActive = true,
        });

        return GetAuthUri(ticket.Id.ToString());
    }

    private string GetAuthUri(string state)
    {
        return $"{AuthorizeEndpoint}{string.Join('&', getParameters())}";

        IEnumerable<string> getParameters()
        {
            foreach (var p in getParameterPairs())
            {
                yield return $"{p.Key}={Uri.EscapeDataString(p.Value)}";
            }
        }

        IEnumerable<KeyValuePair<string, string>> getParameterPairs()
        {
            yield return new("response_type", "code");
            yield return new("client_id", _configuration.ClientId);
            yield return new("scope", string.Join(' ', _configuration.Scopes));
            yield return new("redirect_uri", GetRedirectUri(state));
        }
    }

    public async Task<CompanyCamClient> GetClientAsync(IEntityContext context)
    {
        var integration = await GetIntegrationAsync(context);
        return await GetClientAsync(context, integration);
    }

    public async Task<CCIntegrationConfiguration> GetIntegrationAsync(IEntityContext context)
    {
        var integration = await _connection.Filter<CCIntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, context.GetOwnerEntityId())
            .Eq(x => x.IntegrationId, IntegrationIds.CompanyCam)
            .FirstOrDefaultAsync();
        return integration;
    }

    private async Task<string> UnprotectAsync(IEntityContext context, string protectedString)
    {
        return await _dataProtectionService.UnprotectAsync(
            context,
            new MicrosoftDataProtectionConfig
            {
                Purpose = CCIntegrationConfiguration.ProtectionKey,
            },
            protectedString
        );
    }

    private async Task<CompanyCamClient> GetClientAsync(IEntityContext context, CCIntegrationConfiguration integration)
    {
        if (integration == null) throw NotFoundException.New("CompanyCam Integration not configured");

        var protectedAccessToken = integration.PersonalAccessToken;
        if (string.IsNullOrEmpty(protectedAccessToken))
        {
            if (integration.Token == null) throw new ForbiddenException("Missing Authentication");
            if (integration.Token.ExpiresOn.AddMinutes(10) < DateTime.UtcNow)
            {
                _logger.LogInformation("Refresh Token {EntityId}", context.EntityId);

                var refresh = await RefreshTokenAsync(context, integration);
                if (!refresh.IsSuccess)
                {
                    _logger.LogError("Failed to Refresh Token: {Status}", refresh.Status);
                    throw new ForbiddenException("Failed to refresh token");
                }

                protectedAccessToken = refresh.Value.Token.AccessToken;
            }
            else
            {
                protectedAccessToken = integration.Token.AccessToken;
            }
        }

        var accessToken = await UnprotectAsync(context, protectedAccessToken);

        return new CompanyCamClient(Client, accessToken);
    }

    private async Task<Token> GetTokenAsync(string code, string state)
    {
        var form = new Dictionary<string, string>
        {
            { "client_id", _configuration.ClientId },
            { "client_secret", _configuration.ClientSecret },
            { "grant_type", "authorization_code" },
            { "redirect_uri", GetRedirectUri(state) },
            { "code", code }
        };

        var response = await Client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("Get Token: {Body}", body);

        if (!response.IsSuccessStatusCode)
        {
            throw new BadRequestException("Invalid Authorization Code");
        }

        var token = JsonConvert.DeserializeObject<Token>(body);
        token.ExpiresOn = token.CreatedOn.AddSeconds(token.ExpiresIn);
        return token;
    }

    private async Task<Result<CCIntegrationConfiguration>> RefreshTokenAsync(IEntityContext context, CCIntegrationConfiguration integration)
    {
        var refreshToken = await UnprotectAsync(context, integration.Token.RefreshToken);

        var form = new Dictionary<string, string>
        {
            { "client_id", _configuration.ClientId },
            { "client_secret", _configuration.ClientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        var response = await Client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to Refresh Token: {Body}", body);
            return Result.Error<CCIntegrationConfiguration>("OAuth Token request failed");
        }

        var token = JsonConvert.DeserializeObject<Token>(body);
        token.ExpiresOn = token.CreatedOn.AddSeconds(token.ExpiresIn);

        var result = await UpsertIntegrationAsync(context, token);
        if (result.IsSuccess)
        {
            lock (integration)
            {
                // update token in the previous instance of the integration config 
                // just in case it will be reused
                integration.Token = result.Value.Token;
            }
        }

        return result;
    }

    private string GetRedirectUri(string state)
    {
        return $"{_baseUrl}/companycam/v1/integration/redirect?state={state}";
    }

    public async Task<Result<CCIntegrationConfiguration>> LoginRedirectAsync(Guid ticketId, string code)
    {
        var ticket = await _connection.Filter<SingleUseTicket>()
            .Eq(x => x.Id, ticketId)
            // .Ne(x => x.IsActive, false)
            .Gt(x => x.ExpiresOn, DateTime.UtcNow)
            .Update
            .Set(x => x.IsActive, false)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (ticket == null)
        {
            _logger.LogError("Couldn't find {TicketId}", ticketId);
            return Result.Error<CCIntegrationConfiguration>("Ticket Invalid or Expired");
        }

        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, ticket.AccountId)
            .Eq(x => x.Id, ticket.EntityId)
            .FirstOrDefaultAsync();

        using var scope = _logger.AddScope(new
        {
            EntityId = entity.Id,
            Entity = entity.Name,
            entity.ObjectType,
        });

        _logger.LogInformation("Upsert CompanyCam integration");

        var context = entity.Context;
        var token = await GetTokenAsync(code, ticket.Id.ToString());
        var result = await UpsertIntegrationAsync(context, token);
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to Upsert Integration: {Status}", result.Status);
            return result;
        }

        await AddIdentitiesAsync(entity, result.Value);

        if (string.IsNullOrEmpty(result.Value.WebhookId))
        {
            _logger.LogInformation("Create webhook");
            await CreateWebhookAsync(entity.Context, result.Value);
        }

        // TODO: get users and add identities? 
        // ...

        return result;
    }

    private async Task<bool> AddIdentitiesAsync(Entity entity, CCIntegrationConfiguration integration)
    {
        return entity switch
        {
            Organization organization => await AddIdentityAsync(organization.Context, organization, integration),
            _ => false,
        };
    }

    private async Task<bool> AddIdentityAsync(IEntityContext context, Organization entity, CCIntegrationConfiguration integration)
    {
        if (entity.Identities?.FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.CompanyCam)) != null)
        {
            _logger.LogInformation("{OrganizationId} already has a CompanyCam identity", entity.Id);
            return false;
        }

        _logger.LogInformation("Add CompanyCam identity to {OrganizationId}: {CompanyId}", entity.Id, integration.CompanyId);

        // add identity to organization
        var update = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, entity.AccountId)
            .Eq(x => x.Id, entity.Id)
            .Update
            .AddToSet(x => x.Identities, new EntityIdentity
            {
                Id = Guid.NewGuid(),
                IdentityProviderId = nameof(ExternalProvider.CompanyCam),
                ExternalId = integration.CompanyId,
                Data = new Dictionary<string, object>
                {
                    { "Name", integration.CompanyName }
                },
            })
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateOneAsync();

        if (update.ModifiedCount != 1)
        {
            _logger.LogError("Failed to add CompanyCam identity");
            return false;
        }

        await _objectTypeService.FireObjectUpdatedAsync(context, entity, new Dictionary<string, object>
        {
            { nameof(Entity.Identities), nameof(CompanyCam) }
        }, e =>
        {
            e.Description = "Added CompanyCam Identity";
            e.TryAddMetaValue(nameof(CCIntegrationConfiguration.CompanyId), integration.CompanyId);
            e.TryAddMetaValue(nameof(CCIntegrationConfiguration.CompanyName), integration.CompanyName);
        });

        return true;
    }

    /// <summary>
    /// Upsert integration on the context of the Owner (Account/Organization)
    /// </summary>
    private async Task<Result<CCIntegrationConfiguration>> UpsertIntegrationAsync(IEntityContext context, Token token)
    {
        var client = new CompanyCamClient(Client, token.AccessToken);
        var company = await client.GetCurrentCompanyAsync();

        // update on the context of the owner (e.g. Account/Org)
        var ownerContext = context.GetOwnerEntityContext();
        var objectType = await _objectTypeService.GetAsync(ownerContext, IntegrationObjectTypeName);
        if (objectType == null) throw NotFoundException.New(IntegrationObjectTypeName);
        var result = await _objectTypeService.AddObjectAsync(
            ownerContext,
            objectType,
            new CCIntegrationConfiguration
            {
                Token = token,
                CompanyId = company.Id,
                CompanyName = company.Name,
                Description = $"{company.Name} @ CompanyCam",
                Name = "CompanyCam",
            },
            new ObjectTypeService.AddObjectOptions
            {
                IsUpsert = true
            }
        );

        if (!result.IsSuccess) return result.ConvertTo<CCIntegrationConfiguration>();

        var integration = await _connection.Filter<CCIntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, result.Value.ObjectId)
            .FirstOrDefaultAsync();

        if (integration == null) return Result.Error<CCIntegrationConfiguration>("Couldn't find integration after saving it");

        return Result.Success(integration);
    }

    public async Task<Result<Webhook>> CreateWebhookAsync(IEntityContext context)
    {
        var integration = await GetIntegrationAsync(context);
        if (integration == null) throw NotFoundException.New(nameof(CCIntegrationConfiguration));

        return await CreateWebhookAsync(context, integration);
    }

    public async Task<Result<Webhook>> CreateWebhookAsync(IEntityContext context, CCIntegrationConfiguration integration)
    {
        if (!string.IsNullOrWhiteSpace(integration.WebhookId))
        {
            _logger.LogInformation("Already subscribed");
            return Result.Error<Webhook>("Already subscribed");
        }

        var entityId = context.GetOwnerEntityId();
        var client = await GetClientAsync(context, integration);
        var result = await client.CreateWebhookAsync(new Body18
        {
            Url = $"{_baseUrl}/companycam/v1/Webhook({entityId})",
            Enabled = true,
            Token = entityId.ToString(),
            Scopes = new List<string>
            {
                "*"
            }
        });

        if (result == null) return Result.Error<Webhook>("Failed to create webhook");

        await _connection.Filter<CCIntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, integration.Id)
            .Eq(x => x.WebhookId, null)
            .Update
            .Set(x => x.WebhookId, result.Id)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateOneAsync();

        return Result.Success(result);
    }

    public async Task<Form> GetAddOrEditFormAsync(IEntityContext context)
    {
        var integration = await GetIntegrationAsync(context);
        if (integration != null)
        {
            var objectType = await _objectTypeService.GetAsync(context, IntegrationObjectTypeName);
            var form = await _objectTypeService.GetEditDataFormAsync(context, objectType, integration.Id, objectType.CanUpdate(context) ? FormName.Edit : FormName.Details);
            if (form != null)
            {
                form.Actions = (form.Actions ?? Enumerable.Empty<FormAction>())
                    .Append(new FormAction
                    {
                        Name = "Login",
                        Label = "Reconnect",
                        Action = "Login"
                    })
                    .ToArray();
            }

            return form;
        }

        // Login ("add")
        return new Form
        {
            Name = "CompanyCam",
            Title = "CompanyCam",
            Fields =
            [
                new LabelField
                {
                    Name = "Message",
                    Label = "Connect your CompanyCam account so we can exchange information between the two systems",
                },
                new LabelField
                {
                    Name = "Instructions",
                    Label = "After clicking Start we will open a new tab for you to continue the process",
                }
            ],
            Actions =
            [
                new FormAction
                {
                    Name = "Login",
                    Action = "Login",
                }
            ]
        };
    }

    public async Task<DataFormActionResponse> AddOrEditFormAsync(IEntityContext context, DataFormActionRequest request)
    {
        if (request.Action == "Login")
        {
            var uri = await StartLoginAsync(context);

            return new DataFormActionResponse(request, "Launching on new Browser tab")
            {
                NextUrl = uri,
                Success = true,
            };
        }

        if (request.Action == FormAction.Update)
        {
            return await _objectTypeService.ExecObjectActionAsync(context, IntegrationObjectTypeName, request);
        }

        return DataFormActionResponse.Error(request, "Invalid Action");
    }

    public async Task<Result<string>> GetAssociatedProjectIdAsync(IEntityContext context, string objectTypeName, Guid objectId)
    {
        var objectType = await _objectTypeService.GetAsync(context, objectTypeName);
        if (objectType == null) return Result.Error<string>("Invalid or Missing Object Type");

        return await GetAssociatedProjectIdAsync(context, objectType, objectId);
    }

    public async Task<Result<string>> GetAssociatedProjectIdAsync(IEntityContext context, ObjectType objectType, Guid objectId)
    {
        var obj = await _objectTypeService.GetExpandoObjectByIdAsync(context, objectType, objectId);
        if (obj == null) return Result.Error<string>("Invalid or Missing Object");

        return GetAssociatedProjectIdAsync(context, objectType, obj);
    }

    private static Result<string> GetAssociatedProjectIdAsync(IEntityContext context, ObjectType objectType, ExpandoObject obj)
    {
        var field = objectType.Fields.FirstOrDefault(x => x.Value.Field is ReferenceField referenceField && referenceField.ReferenceFieldOptions?.ObjectType == "companycam.Project").Value;
        if (field == null || !field.RBAC.CanRead(context)) return Result.Error<string>("No field found or can't update it");
        
        if (!obj.TryGetFieldValue(field.Field.Name, out var current) || current == null) return Result.Error<string>("Not associated with a project");

        if (current is not string currentStr) return Result.Error<string>("Invalid value");

        return Result.Success(currentStr);
    }

    public async Task<Result<CCProject>> AssociateAsync(IEntityContext context, string objectTypeName, Guid objectId, string projectId)
    {
        var objectType = await _objectTypeService.GetAsync(context, objectTypeName);
        if (objectType == null) return Result.Error<CCProject>("Invalid or Missing Object Type");

        var field = objectType.Fields.FirstOrDefault(x => x.Value.Field is ReferenceField referenceField && referenceField.ReferenceFieldOptions?.ObjectType == "companycam.Project").Value;
        if (field == null) return Result.Error<CCProject>("No field found or can't update it");

        // ideally we would check the context has access, but since we don't want it to be modified directly skipping it
        // !field.RBAC.CanUpdate(context)

        var obj = await _objectTypeService.GetExpandoObjectByIdAsync(context, objectType, objectId);
        if (obj == null) return Result.Error<CCProject>("Invalid or Missing Object");

        var project = await _connection.Filter<CCProject>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.ExternalId, projectId)
            .FirstOrDefaultAsync();

        if (obj.TryGetFieldValue(field.Field.Name, out var current) && current != null)
        {
            if (current is string currentStr && currentStr == projectId) return Result.Success(project);
            return Result.Error<CCProject>("Can't modify existing association");
        }

        if (project == null)
        {
            var client = await GetClientAsync(context);
            client.ReadResponseAsString = true;

            var ccproject = await client.GetProjectAsync(projectId);
            if (ccproject == null) return Result.Error<CCProject>("CompanyCam Project not found");

            var newId = Guid.NewGuid();
            project = await _connection.Filter<CCProject>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.EntityId, context.OrganizationId.Value)
                .Eq(x => x.ExternalId, projectId)
                .Update
                .SetOnInsert(x => x.Id, newId)
                .SetOnInsert(x => x.AccountId, context.AccountId)
                .SetOnInsert(x => x.EntityId, context.EntityId)
                .SetOnInsert(x => x.ExternalId, projectId)
                .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
                .Set(x => x.Properties, JsonObjectConverter.Convert<ExpandoObject>(ccproject))
                .Set(x => x.LastActor, context.Actor())
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateAndGetOneAsync(true);

            // fire event
            if (project.Id == newId)
            {
                // created
                await _objectTypeService.FireCreateEventAsync(context, project, e => { e.Description = "Imported by User"; });
            }
            else
            {
                // updated
                await _objectTypeService.FireObjectUpdatedAsync(context, project, new Dictionary<string, object>
                {
                    { nameof(CCProject.Properties), "*" }
                });
            }
        }

        await _objectTypeService.UpdateObjectAsync(context, objectType, objectId, q => q.Update.Set(FormField.GetPathInCollection(field.Field.Name), projectId), new Dictionary<string, object>());

        return Result.Success(project);
    }
}