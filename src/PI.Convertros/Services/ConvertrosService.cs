using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Controllers;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Driver;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Models;
using PI.Shared.Services;
using LeadRequest = Models.LeadRequest;

namespace Services;

public class ConvertrosService : AbstractLeadConversionIntegrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    public const string ConvertrosProvider = "Convertros";
    
    private HttpClient Client => _httpClientFactory.CreateClient("Convertros");
    private readonly Config _config;

    public override Guid IntegrationId => IntegrationIds.Convertros;
    public override string ClientId => nameof(IntegrationIds.Convertros);

    public ConvertrosService(
        ILogger<ConvertrosService> logger,
        MongoConnection connection,
        AuthorizationService authorizationService,
        ObjectTypeService objectTypeService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory
    ) :
        base(logger, connection, authorizationService, objectTypeService, configuration)
    {
        _httpClientFactory = httpClientFactory;
        _config = configuration.GetSection(nameof(ConvertrosService)).Get<Config>();
    }

    public override async Task<IResult> ConditionallyPostLeadAsync(Lead lead)
    {
        return await PostLeadAsync(lead);
    }

    private async Task<Lead> EndConverationAsync(Lead lead, Guid externalId, bool qualify = false)
    {
        _logger.LogInformation("End Conversation for {ExternalId}: {Qualify}", externalId, qualify);

        var response = await PostLeadAsync(lead);
        if (response.IsSuccess) return null;

        return await SetCancelledOnAsync(lead, qualify ? "Cancelled (Opt Out)" : "Cancelled (Converted)");
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

        _logger.LogInformation("Send Lead to {Integration}", IntegrationIds.GetName(IntegrationId));

        var organization = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.Id, lead.EntityId)
            .IncludeField("_t")
            .IncludeField(x => x.Name)
            .IncludeField(x => x.Identities)
            .IncludeField(x => x.Properties)
            .FirstOrDefaultAsync();

        var inspirenetOrg = organization?.Identities?.FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.InspireNet));
        var sfOrg = organization?.Identities?.FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Salesforce));
        if (inspirenetOrg == null || sfOrg?.Data == null)
        {
            _logger.LogError("Failed to get Organization or identities not found");
            await AddIntegrationAsync(lead, "Error (failed to load Organization)");
            return Result.Error("Failed to get organization for lead");
        }
        
        // TODO: should move the opt out to the main object 
        // right now uses data from sf, that depending on how it got "here" may be in different format
        // ...
        
        var optOutLeadConversion = false;
        if (sfOrg.Data.TryGetValue("Opt_Out_of_Lead_Conversion__c", out var optOutLeadConversionObj) || sfOrg.Data.TryGetValue("optOutOfLeadConversionC", out optOutLeadConversionObj))
        {
            optOutLeadConversion = optOutLeadConversionObj switch
            {
                bool b => b,
                string str => bool.TryParse(str, out var b) && b,
                _ => false
            };            
        }

        var optOutCallcenter = false;
        if (sfOrg.Data.TryGetValue("Opt_Out_of_Call_Center__c", out var optOutCallcenterObj) || sfOrg.Data.TryGetValue("optOutOfCallCenterC", out optOutCallcenterObj))
        {
            optOutCallcenter = optOutCallcenterObj switch
            {
                bool b => b,
                string str => bool.TryParse(str, out var b) && b,
                _ => false
            };            
        } 

        if (optOutLeadConversion || optOutCallcenter)
        {
            _logger.LogInformation("Organization has Opted Out of Callcenter");
            await AddIntegrationAsync(lead, "Skip (Organization Opted Out)");

            return Result.Unknown("Organization has opted out");
        }

        var doNotContact = !lead.IsActive ||
                           lead.GetCommunicationPreference(CommunicationChannel.SMS) == CommunicationPreference.OptedOut ||
                           lead.GetCommunicationPreference(CommunicationChannel.Email) == CommunicationPreference.OptedOut ||
                           lead.GetCommunicationPreference(CommunicationChannel.Phone) == CommunicationPreference.OptedOut;

        var integrations = lead.Integrations ?? Enumerable.Empty<LeadIntegration>();
        var converted = lead.ConvertedOn.HasValue ||
                        integrations.Any(x => x.IntegrationId == IntegrationIds.Salesforce && x.Tag == "Account");

        var sfLead = integrations.FirstOrDefault(x => x.IntegrationId == IntegrationIds.Salesforce && x.Tag == "Lead");

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

        var leadType = await _connection.Filter<LeadType>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.Id, lead.LeadTypeId)
            .FirstOrDefaultAsync();

        var authorization = CreateAuthorizationToken(lead, 365);

        var existingIntegration = lead.Integrations?.FirstOrDefault(x => x.IntegrationId == IntegrationId);

        var api = new LeadRequest
        {
            Id = existingIntegration?.ExternalId != null ? Guid.Parse(existingIntegration.ExternalId) : null,
            LeadName = lead.Name,
            FirstName = lead.FirstName,
            LastName = lead.LastName,
            LeadState = doNotContact ? "DNC" : (converted ? "converted" : "active"),
            EMail = lead.Email,
            Phone = lead.Phone,
            LocationNumber = inspirenetOrg.ExternalId,
            LocationName = organization.Name,
            LeadLink = sfLead?.ExternalId != null && _config.SalesforceTemplateUrl != null ? _config.SalesforceTemplateUrl.Replace("{id}", sfLead.ExternalId) : null,
            Addres_Line1 = lead.Address,
            Addres_City = lead.City,
            Addres_PostalCode = lead.PostalCode,
            Addres_State = lead.State,
            Source = leadType?.Name,
            SourceInternalId = lead.Id.ToString(),
            Tags = _config.Tags,
            // SourceCampaign
            // SourceKeywords
            // CompanyName = null,
            // Phone2
            // Addres_Line2 = 
            // Notes =

            SchedulerURL = _config.SchedulerTemplateUrl.Replace("{auth}", authorization),
            Authorization = authorization,
        };

        var response = await SendLeadAsync(api, _config.LeadUrl);

        if (existingIntegration == null)
        {
            await AddIntegrationAsync(lead, response ? "Exported" : response.Status, response.Value?.Id?.ToString());
        }

        return response;
    }

    private async Task<Lead> AddIntegrationAsync(Lead lead, string status, string externalId = null)
    {
        if (lead.Integrations?.Any(x => x.IntegrationId == IntegrationId) ?? false)
        {
            // already has the integration
            return lead;
        }

        // var integration = lead.Integrations?.FirstOrDefault(x => x.IntegrationId == Consts.IntegrationId);
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
                Url = externalId != null && _config.IntegrationTemplateUrl != null ? _config.IntegrationTemplateUrl.Replace("{externalId}", externalId) : null,
            })
            .UpdateAndGetOneAsync();

        return lead;
    }

    private async Task<Result<LeadResponse>> SendLeadAsync(LeadRequest lead, string url)
    {
        var response = await PostAsync<LeadRequest, LeadResponse>(url, lead);
        if (!response) return response;

        // if (response.Value.Status != "success")
        // {
        //     _logger.LogError("Received non-successful {Status}", response.Status);
        //     return Result.Error<Models.VerseResponse>($"Received status: {response.Status}");
        // }

        _logger.LogInformation("Exported success¡fully with {ExternalId}", response.Value.Id);

        return response;
    }

    private async Task<Result<TResp>> PostAsync<TReq, TResp>(string url, TReq body)
    {
        var json = JsonConvert.SerializeObject(body, SerializationSettings.Default);
        _logger.LogInformation("Post {Url}: {Body}", url, json);

        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await Client.PostAsync(url, httpContent);
        string respBody;
        try
        {
            respBody = await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load response ({StatusCode})", resp.StatusCode);
            return Result.Error<TResp>("Failed to load response from request.");
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

            _logger.LogError("Request failed with {StatusCode}: {Body}", resp.StatusCode, body);
            return Result.Error<TResp>($"Request Failed with {resp.StatusCode}");
        }

        _logger.LogInformation("Request successfull: {Response}", body);

        try
        {
            var response = JsonConvert.DeserializeObject<TResp>(respBody);
            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse successfull response: {Body}", body);
            return Result.Error<TResp>("Failed to parse response.");
        }
    }

    public class Config
    {
        public string LeadUrl { get; set; } = "https://convertrosfci.azurewebsites.net/api/FCILeadTest";
        public string SchedulerTemplateUrl { get; set; } = "https://schedule2.fci.cloud/nav.html?auth={auth}";
        public string SalesforceTemplateUrl { get; set; } = "https://fcifloors--fcistaging.my.salesforce.com/{id}";
        public string IntegrationTemplateUrl { get; set; }
        public string Tags { get; set; } = "Testing";

        public Guid? CommunicationNoteFlowId { get; set; }
        public Guid? CommunicationNoteObjectStatusId { get; set; }
    }

    public async Task<long> ImportNotesAsync(IEntityContext context)
    {
        _logger.LogInformation("Started Import");
        var cursor = _connection.Filter<CallLog>().SortAsc(x => x.CreatedOn).WithBatchSize(500).ToCursor(); 
        var count = 0L;
        while (await cursor.MoveNextAsync())
        {
            _logger.LogInformation("handle page: {Records}", count);
            
            var batch = cursor.Current.ToArray();
            await UpsertCommunicationNotesAsync(context, batch);
            count += batch.Length;
            
            _logger.LogInformation("saved page");
        }
        
        _logger.LogInformation("Imported {Count}", count);

        return count;
    }    
    
    public async Task UpsertCommunicationNotesAsync(IEntityContext context, CallLog[] request)
    {
        // communication notes?
        var leads = (await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, AccountIds.FCI)
            .In(x => x.Id, request.Select(x => x.LeadId))
            .FindAsync()).ToDictionary(x => x.Id, x => x.EntityId);

        await _connection.BulkWriteAsync(request.Select(convert));

        UpdateOneModel<CommunicationNote> convert(CallLog call)
        {
            var phoneNumber = Lead.GetNormalizedPhoneNumber(call.PhoneNumber);
            var direction = call.Direction == "O" ? CommunicationDirection.Outbound : CommunicationDirection.Inbound;
            var now = DateTime.UtcNow;

            var update = _connection.Filter<CommunicationNote>()
                    .Eq(x => x.AccountId, AccountIds.FCI)
                    .Eq(x => x.Provider, ConvertrosProvider)
                    .Eq(x => x.ExternalId, call.Id)
                    .Update
                    .SetOnInsert("_t", "communication")
                    .SetOnInsert(x => x.Id, Guid.NewGuid())
                    .SetOnInsert(x => x.AccountId, AccountIds.FCI)
                    .SetOnInsert(x => x.Provider, ConvertrosProvider)
                    .SetOnInsert(x => x.ExternalId, call.Id)
                    .SetOnInsert(x => x.EntityId, leads.TryGetValue(call.LeadId, out var entityId) ? entityId : AccountIds.FCI)
                    .SetOnInsert(x => x.Direction, direction)
                    .SetOnInsert(x => x.CommunicationChannel, CommunicationChannel.Phone)
                    .SetOnInsert(x => x.CreatedOn, call.Date)
                    .SetOnInsert(x => x.Refs, new List<KeyValuePair<string, object>>
                    {
                        new($"{nameof(Lead)}Id", call.LeadId),
                        new("PhoneNumber", phoneNumber),
                        new("convertros|CallLogId", call.Id),
                    })
                    .Set(x => x.LastModifiedOn, now)
                    .Set(x => x.LastActor, context.Actor())
                    .Set(x => x.Status, CommunicationNote.CompletedStatus)
                    .Set(x => x.Meta, new Dictionary<string, object>
                    {
                        { nameof(CallLog.DispositionCode), call.DispositionCode },
                        { nameof(CallLog.DispositionName), call.DispositionName },
                        { "PhoneNumber", phoneNumber },
                    })
                    .Set(x => x.Milestones, new Dictionary<string, DateTime>
                    {
                        { CommunicationNote.CompletedStatus, call.Date }
                    })
                    // .Set(x => x.ContentFormat, ContentFormat.PlainText)
                    .Set(x => x.ContentType, "text/plain")
                    .Set(x => x.Content, call.DispositionName)
                ;

            if (_config != null)
            {
                if (_config.CommunicationNoteFlowId.HasValue) update.SetOnInsert(x => x.FlowId, _config.CommunicationNoteFlowId);
                if (_config.CommunicationNoteObjectStatusId.HasValue) update.SetOnInsert(x => x.FlowId, _config.CommunicationNoteObjectStatusId);
            }

            if (direction == CommunicationDirection.Outbound)
            {
                update
                    .Set(x => x.Name, $"Callcenter Called {call.PhoneNumber}")
                    .Set(x => x.Description, $"Convertros called {call.PhoneNumber}")
                    .SetOnInsert(x => x.Parties, new[]
                    {
                        new CommunicationParty
                        {
                            Direction = CommunicationDirection.Outbound,
                            Address = ConvertrosProvider,
                        },
                        new CommunicationParty
                        {
                            Direction = CommunicationDirection.Inbound,
                            Address = phoneNumber,
                        }
                    });
            }
            else
            {
                update
                    .Set(x => x.Name, $"Callcenter Received Call from {call.PhoneNumber}")
                    .Set(x => x.Description, $"Convertros received call from {call.PhoneNumber}")
                    .SetOnInsert(x => x.Parties, new[]
                    {
                        new CommunicationParty
                        {
                            Direction = CommunicationDirection.Outbound,
                            Address = phoneNumber,
                        },
                        new CommunicationParty
                        {
                            Direction = CommunicationDirection.Inbound,
                            Address = ConvertrosProvider,
                        }
                    });
            }

            return update.UpdateOneModel(true);
        }
    }
}