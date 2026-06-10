using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Dipper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

/// <summary>
/// https://developer.verse.io/
/// </summary>
public class VerseService : AbstractLeadConversionIntegrationService
{
    private readonly IMapper _mapper;
    private readonly IHttpClientFactory _httpClientFactory;

    private HttpClient Client
    {
        get
        {
            var client = _httpClientFactory.CreateClient(); 
            client.DefaultRequestHeaders.Add("X-API-KEY", _config.ApiKey);
            return client;
        }
    }
    
    private readonly Config _config;

    public VerseService(
        ILogger<VerseService> logger,
        MongoConnection connection,
        AuthorizationService authorizationService,
        ObjectTypeService objectTypeService,
        IConfiguration configuration,
        IMapper mapper,
        IHttpClientFactory httpClientFactory
    ) :
        base(logger, connection, authorizationService, objectTypeService, configuration)
    {
        _mapper = mapper;
        _httpClientFactory = httpClientFactory;
        _config = configuration.GetSection(nameof(VerseService)).Get<Config>();
    }

    public override Guid IntegrationId => IntegrationIds.Verse;
    public override string ClientId => "Verse.io";

    public async Task ProcessAsync(VerseEvent body)
    {
        if (body.CustomFields == null || !(body.CustomFields.TryGetValue("Authorization", out var authorization) || body.CustomFields.TryGetValue("authorization", out authorization)))
        {
            throw new ForbiddenException("Missing authorization");
        }

        var principal = _authorizationService.ValidateToken(authorization);
        var context = principal.GetEntityContextWithActor();
        var claim = principal.Claims.FirstOrDefault(c => c.Type == "pi_lead_id");
        if (claim == null) throw new ForbiddenException("Missing");
        var leadId = Guid.Parse(claim.Value);

        if (leadId != body.ExternalLeadID) throw new ForbiddenException("Mismatch");

        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, leadId)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q.Eq(x => x.IntegrationId, IntegrationId)
            )
            .FirstOrDefaultAsync();

        if (lead == null) throw NotFoundException.New<Lead>(leadId);

        using var scope = _logger.AddScope(new
        {
            LeadId = lead.Id,
            Lead = lead.Name,
            lead.AccountId,
            lead.EntityId,
            body.Title,
            body.Event,
        });

        _logger.LogInformation("Received event");

        switch (body.Title)
        {
            case VerseTitle.LeadCreated:
                await SetStatusAsync(context, lead, "Received", nameof(VerseLeadIntegration.Received));
                break;

            case VerseTitle.QualifiedLead:
                // TODO: UPDATE LEAD ADDRESS/CONTACT
                // ...
                await SetStatusAsync(context, lead, "Qualified", nameof(VerseLeadIntegration.Converted));
                break;

            case VerseTitle.UnqualifiedLead:
                await AddNoteAsync(context, lead, body);
                await SetStatusAsync(context, lead, $"Unqualified: {body.ReasonUnqualified}", nameof(VerseLeadIntegration.OptOut), true);
                // TODO: fire event
                // ...
                break;

            case VerseTitle.InboundCallReceived:
            case VerseTitle.InboundEmail:
            case VerseTitle.InboundSMS:
            case VerseTitle.OutboundEmail:
            case VerseTitle.OutboundSMS:
            case VerseTitle.OutboundCallAttempt:
                await AddCommunicationAsync(context, lead, body);
                break;

            case VerseTitle.CallForwarded:
            case VerseTitle.LiveTransferAttempt:
            case VerseTitle.LiveTransferSuccessful:
            case VerseTitle.LiveTransferUnsuccessful:
            case VerseTitle.VerseActivityLog:
            case VerseTitle.ConciergeNote:
            default:
                await AddNoteAsync(context, lead, body);
                break;
        }
    }

    private async Task AddCommunicationAsync(IContextWithActor context, Lead lead, VerseEvent body)
    {
        var channel = body.Title switch
        {
            VerseTitle.InboundCallReceived => CommunicationChannel.Phone,
            VerseTitle.InboundEmail => CommunicationChannel.Email,
            VerseTitle.InboundSMS => CommunicationChannel.SMS,
            VerseTitle.OutboundEmail => CommunicationChannel.Email,
            VerseTitle.OutboundSMS => CommunicationChannel.SMS,
            VerseTitle.OutboundCallAttempt => CommunicationChannel.Phone,
            _ => "Unknown"
        };

        var direction = body.Title switch
        {
            VerseTitle.InboundCallReceived => CommunicationDirection.Inbound,
            VerseTitle.InboundEmail => CommunicationDirection.Inbound,
            VerseTitle.InboundSMS => CommunicationDirection.Inbound,
            VerseTitle.OutboundEmail => CommunicationDirection.Outbound,
            VerseTitle.OutboundSMS => CommunicationDirection.Outbound,
            VerseTitle.OutboundCallAttempt => CommunicationDirection.Outbound,
            _ => CommunicationDirection.Unknown,
        };

        var address = channel switch
        {
            CommunicationChannel.Email => body.Email,
            CommunicationChannel.Phone => body.Phone,
            CommunicationChannel.SMS => body.Phone,
            _ => null
        };

        var prefix = body.Title + ": ";
        var content = body.Message.StartsWith(prefix) ? body.Message[prefix.Length..] : body.Message;

        var note = new CommunicationNote
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            AccountId = lead.AccountId,
            EntityId = lead.EntityId,
            CommunicationChannel = channel,
            Direction = direction,
            Parties = new[]
            {
                new CommunicationParty
                {
                    Direction = direction,
                    Address = body.Phone,
                }
            },
            Name = body.Title,
            Description = content,
            Content = content,
            // ContentFormat = ContentFormat.PlainText,
            ContentType = "text/plain",
            Refs = new List<KeyValuePair<string, object>>
            {
                new($"{nameof(Lead)}Id", lead.Id),
                new($"{nameof(Integration)}Id", IntegrationId),
            },
        };

        await AddAsync(context, lead, note);

        await SetStatusAsync(
            context,
            lead,
            direction switch
            {
                CommunicationDirection.Inbound => channel switch
                {
                    CommunicationChannel.Email => "Received Email",
                    CommunicationChannel.Phone => "Received Call",
                    CommunicationChannel.SMS => "Received SMS",
                    _ => channel,
                },
                CommunicationDirection.Outbound => channel switch
                {
                    CommunicationChannel.Email => "Sent Email",
                    CommunicationChannel.Phone => "Called",
                    CommunicationChannel.SMS => "Sent SMS",
                    _ => channel,
                },
                _ => direction.ToString(),
            },
            direction switch
            {
                CommunicationDirection.Inbound => nameof(VerseLeadIntegration.FirstResponse),
                CommunicationDirection.Outbound => nameof(VerseLeadIntegration.ReachedOut),
                _ => direction.ToString(),
            }
        );
    }

    private async Task AddNoteAsync(IContextWithActor context, Lead lead, VerseEvent body)
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            AccountId = lead.AccountId,
            EntityId = lead.EntityId,
            Content = body.Message,
            // ContentFormat = ContentFormat.PlainText,
            ContentType = "text/plain",
            Name = body.Title,
            Description = body.Message,
            Refs = new List<KeyValuePair<string, object>>
            {
                new($"{nameof(Lead)}Id", lead.Id),
                new($"{nameof(Integration)}Id", IntegrationId),
            },
        };

        await AddAsync(context, lead, note);
    }

    private async Task SetStatusAsync(IEntityContext context, Lead lead, string status, string milestone, bool optOut = false)
    {
        var now = DateTime.UtcNow;
        
        var query = _connection.Filter<Lead>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, lead.Id)
                .ElemMatchBuilder(
                    x => x.Integrations,
                    q => q
                        .Eq(x => x.IntegrationId, IntegrationId)
                        .Eq(milestone, default(DateTime?))
                )
                .Update
                .Set($"{nameof(Lead.Integrations)}.$.{nameof(LeadIntegration.Status)}", status)
                .Set($"{nameof(Lead.Integrations)}.$.{milestone}", now)
                .Set(x => x.LastModifiedOn, now)
                .Set(x => x.LastActor, context.Actor())
            ;

        // if (optOut)
        // {
        //     // change comm preferences
        //     query.Set(x => x.CommunicationPreferences[CommunicationChannel.SMS], CommunicationPreference.OptedOut);
        // }

        var result = await query.UpdateAndGetOneAsync();

        if (result == null)
        {
            _logger.LogInformation("did not change status");
            return;
        }

        _logger.LogInformation("Changed status to {Status} / {OptOut}", status, optOut);

        var modifiedFields = new Dictionary<string, object>
        {
            { $"{nameof(Lead.Integrations)}|{IntegrationId}|{nameof(LeadIntegration.Status)}", status },
            { $"{nameof(Lead.Integrations)}|{IntegrationId}|{milestone}", now },
        };

        await _objectTypeService.FireObjectUpdatedAsync(context, result, modifiedFields, evt =>
        {
            evt.Description = $"Verse changed status to {status}";
            evt.SetRefValue(nameof(Integration), IntegrationId);
            evt.SetMetaValue(nameof(Integration), IntegrationIds.GetName(IntegrationId));
            evt.SetMetaValue(milestone, true);
            if (optOut) evt.SetMetaValue("OptOut", optOut);
        });
    }

    public async Task<JobResult> SendLeadsAsync(IEntityContext context)
    {
        var leads = await _connection.DipperAggregateAsync<Record>("VerseLeads", context.AccountId.Value.ToString("N"));

        var success = 0;
        var failed = 0;
        foreach (var lead in leads)
        {
            var pair = await PostLeadAsync(lead);
            if (pair.IsSuccess)
            {
                success++;
            }
            else if (pair.IsError)
            {
                failed++;
            }
        }

        var message = leads.Count switch
        {
            0 => "Nothing to export",
            1 => success == 1 ? "Exported one lead to Verse" : "Failed to export lead to Verse",
            _ when success == leads.Count => $"{success} leads exported to Verse successfully",
            _ => $"{success} out of {leads.Count} leads exported to Verse successfully. {failed} Failed.",
        };

        return new JobResult
        {
            Message = message,
            Result = new Dictionary<string, object>
            {
                { "Total", leads.Count },
                { "Exported", success },
                { "Skipped", (leads.Count - success - failed) },
                { "Errors", failed },
            }
        };
    }

    public async Task<Result<Models.VerseLead>> EndConverationAsync(Guid verseLeadId, bool qualify = false)
    {
        _logger.LogInformation("End Conversation for {VerseLeadId}: {Qualify}", verseLeadId, qualify);

        var url = qualify ?
            _config.LeadQualifiedUrl :
            _config.LeadUnqualifiedUrl;

        var body = new Models.VerseSearch
        {
            SearchValue = verseLeadId.ToString(),
        };

        return await PostAsync<Models.VerseSearch, Models.VerseLead>(url, body);
    }

    public override async Task<IResult> ConditionallyPostLeadAsync(Lead lead)
    {
        var doNotContact = !lead.IsActive ||
                           lead.GetCommunicationPreference(CommunicationChannel.SMS) == CommunicationPreference.OptedOut ||
                           lead.GetCommunicationPreference(CommunicationChannel.Email) == CommunicationPreference.OptedOut ||
                           lead.GetCommunicationPreference(CommunicationChannel.Phone) == CommunicationPreference.OptedOut;

        var integrations = lead.Integrations ?? Enumerable.Empty<LeadIntegration>();
        var converted = lead.ConvertedOn.HasValue ||
                        integrations.Any(x => x.IntegrationId == IntegrationIds.Salesforce && x.Tag == "Account");

        var nextAppt = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.LeadId, lead.Id)
            .Eq(x => x.CancelledOn, null)
            .Ne(x => x.IsActive, false)
            .Gt(x => x.Start, DateTime.UtcNow)
            .SortAsc(x => x.Start)
            .FirstOrDefaultAsync();

        if (nextAppt != null && !converted)
        {
            // lead hasn't been officially converted yet, but there is an appt.
            _logger.LogInformation("There is an appt but lead hasn't been converted yet, implicitly assuming conversion");
            converted = true;
        }

        var integration = integrations.FirstOrDefault(x => x.IntegrationId == IntegrationId);
        if (integration != null)
        {
            var verseIntegration = integrations.Where(x => x.IntegrationId == IntegrationId)
                .OfType<VerseLeadIntegration>()
                .FirstOrDefault();

            if (verseIntegration == null)
            {
                _logger.LogError("Verse integration not of right type");
            }
            else if (verseIntegration.CancelledOn.HasValue)
            {
                _logger.LogInformation("Integration has been {CancelledOn}, skip sending updates", verseIntegration.CancelledOn);
                return Result.Unknown<Models.VerseResponse>("Ignore (Cancelled)");
            }

            if (integration.ExternalId == null || !Guid.TryParse(integration.ExternalId, out var verseLeadId))
            {
                _logger.LogInformation("Missing verse {ExternalId} for {LeadId}. Ignore updated", lead.Id);
                return Result.Unknown<Models.VerseResponse>("Missing external id, ignore update");
            }

            if (doNotContact)
            {
                _logger.LogInformation("Opt out: DNC");
                var verseLead = await EndConverationAsync(verseLeadId);
                if (verseLead?.IsSuccess ?? false)
                {
                    await SetCancelledOnAsync(lead, "Cancelled (Opt Out)");
                }

                return verseLead;
            }

            if (converted)
            {
                if (nextAppt?.Tool == ClientId)
                {
                    _logger.LogInformation("Ignore Converted event since the appt was booked by Verse.io");
                    return Result.Unknown<Models.VerseResponse>("Ignore (Converted by Verse.io)");
                }

                _logger.LogInformation("Opt out: Converted");

                var verseLead = await EndConverationAsync(verseLeadId, true);
                if (verseLead?.IsSuccess ?? false)
                {
                    await SetCancelledOnAsync(lead, "Cancelled (Converted)");
                }

                return verseLead;
            }

            _logger.LogInformation("Ignore update, no action to report for {LeadId}", lead.Id);
            return Result.Unknown<Models.VerseResponse>("Ignore (No meaningful change)");
        }

        if (doNotContact || converted)
        {
            return Result.Unknown<Models.VerseResponse>("Already converted or opted out, ignore");
        }

        var organization = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.Id, lead.EntityId)
            .IncludeField("_t")
            .IncludeField(x => x.Name)
            .IncludeField(x => x.Identities)
            .IncludeField(x => x.Properties)
            .FirstOrDefaultAsync();

        if (organization == null)
        {
            _logger.LogError("Failed to get {OrganizationId} for {LeadId}", lead.EntityId, lead.Id);
            await AddIntegrationAsync(lead, "Error (failed to load Organization)");
            return Result.Error<string>("Failed to get organization for lead");
        }

        // if (organization.Properties != null && organization.Properties.TryGetValue("LuminOptOut", out var luminOptOutObj) && (luminOptOutObj is bool luminOptOut) && luminOptOut)
        // {
        //     _logger.LogInformation("Ignore {leadId}, {organizationId} has opted out", lead.Id, organization.Id);
        //     await AddIntegrationAsync(lead, "Skipped (Organization Opted Out)");
        //     return Result.Success(string.Empty, "Skip: Organization Opted Out");
        // }

        return await PostLeadAsync(lead);
    }

    private async Task<IResult> PostLeadAsync(Lead lead)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId = lead.Id,
            Lead = lead.Name,
            lead.AccountId,
            lead.EntityId,
        });

        _logger.LogInformation("Send Lead to Verse");

        var authorization = CreateAuthorizationToken(lead, 365);

        var api = _mapper.Map<Models.VerseLead>(lead);
        api.SchedulerUrl = _config.SchedulerTemplateUrl.Replace("{auth}", authorization);

        if (lead is Record record)
        {
            _logger.LogInformation("Overriding agent: {UserId} {User} {Phone} {Email}", record.UserId, record.UserName, record.UserPhone, record.UserEmail);

            api.AgentEmail = record.UserEmail ?? record.Email;
            api.AgentFirstName = NameFromParts.GetFirstName(record.UserName);
            api.AgentLastName = NameFromParts.GetLastName(record.UserName);
            api.AgentPhone = record.UserPhone;
        }

        api.CustomFields = new Dictionary<string, string>
        {
            { "Authorization", authorization }
        };

        var response = await SendLeadAsync(api, _config.LeadUrl);
        await AddIntegrationAsync(lead, response ? "Exported" : response.Status, response.Value?.Id?.ToString());

        return response;
    }

    private async Task<Lead> AddIntegrationAsync(Lead lead, string status, string externalId = null)
    {
        // var integration = lead.Integrations?.FirstOrDefault(x => x.IntegrationId == IntegrationId);
        // if (integration != null)
        // {
        //     // ...
        // }

        lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.Id, lead.Id)
            .Update
            .Push(x => x.Integrations, new LeadIntegration
            {
                IntegrationId = IntegrationId,
                ExternalId = externalId,
                CreatedOn = DateTime.UtcNow,
                Status = status,
                Url = externalId != null ? $"https://app.verse.io/leads/{externalId}" : null,
            })
            .UpdateAndGetOneAsync();

        return lead;
    }

    private async Task<Result<Models.VerseResponse>> SendLeadAsync(Models.VerseLead lead, string url)
    {
        var response = await PostAsync<Models.VerseLead, Models.VerseResponse>(url, lead);
        if (!response) return response;

        if (response.Value.Status != "success")
        {
            _logger.LogError("{LeadId}: Received non-successful {Status}", lead.Id, response.Status);
            return Result.Error<Models.VerseResponse>($"Received status: {response.Status}");
        }

        _logger.LogInformation("{LeadId}: exported successfully with {VerseLeadId}", lead.Id, response.Value.Id);

        return response;
    }

    private async Task<Result<TResp>> PostAsync<TReq, TResp>(string url, TReq body)
    {
        var json = JsonConvert.SerializeObject(body, SerializationSettings.Default);
        _logger.LogInformation("Post {Url}: {Body}", url, json);

        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await Client.PostAsync(url, httpContent);
        var respBody = default(string);
        try
        {
            respBody = await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load response ({StatusCode})", resp.StatusCode);
            return Result.Error<TResp>($"Failed to load response from request.");
        }

        if (!resp.IsSuccessStatusCode)
        {
            // 409 
            // ...
            // {
            //     "status": {
            //         "code": 409,
            //         "message": "Conflict"
            //     },
            //     "code": 110,
            //     "message": "Duplicate"
            // }            

            _logger.LogError("Request failed with {StatusCode}: {Body}", resp.StatusCode, respBody);
            return Result.Error<TResp>($"Request Failed with {resp.StatusCode}");
        }

        _logger.LogInformation("Request successfull: {Response}", respBody);

        try
        {
            var response = JsonConvert.DeserializeObject<TResp>(respBody);
            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse successfull response: {Body}", respBody);
            return Result.Error<TResp>($"Failed to parse response.");
        }
    }

    public class Config
    {
        public string ApiKey { get; set; }
        public string SchedulerTemplateUrl { get; set; }
        public string LeadUrl { get; set; }
        public string LeadQualifiedUrl { get; set; }
        public string LeadUnqualifiedUrl { get; set; }
    }

    public class Record : Lead
    {
        public Guid OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public Guid UserId { get; set; }
        public string UserPhone { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
    }
}