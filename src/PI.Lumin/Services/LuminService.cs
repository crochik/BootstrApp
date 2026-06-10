using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Crochik.Messaging;
using Crochik.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class LuminService : AbstractLeadConversionIntegrationService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public static class Action
    {
        public const string Converted = "already-scheduled";

        // (means that SS should not schedule meeting because it already happened)
        public const string AppointmentHappened = "already-met";

        public const string NoShow = "no-show";

        public const string OptedOut = "opt-out";
        public const string DeadLeadVendor = "dead-lead-vendor";
    }

    private readonly Config _config;

    private HttpClient Client
    {
        get
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.Key);
            return client;
        }
    }

    public override Guid IntegrationId => IntegrationIds.Lumin;
    public override string ClientId => nameof(IntegrationIds.Lumin);

    public LuminService(
        ILogger<LuminService> logger,
        MongoConnection connection,
        AuthorizationService authorizationService,
        ObjectTypeService objectTypeService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory) :
        base(logger, connection, authorizationService, objectTypeService, configuration)
    {
        _httpClientFactory = httpClientFactory;
        _config = configuration.GetSection(nameof(LuminService)).Get<Config>();
    }

    public override async Task<IResult> ConditionallyPostLeadAsync(Lead lead)
    {
        var doNotContact = !lead.IsActive ||
                           lead.GetCommunicationPreference(CommunicationChannel.SMS) == CommunicationPreference.OptedOut ||
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

        var action = default(string);

        var alreadyExported = integrations.Any(x => x.IntegrationId == IntegrationIds.Lumin);
        if (!alreadyExported)
        {
            if (doNotContact)
            {
                _logger.LogInformation("Skip exporting {LeadId} to Lumin as it has opted out already", lead.Id);
                await AddIntegrationAsync(lead, "Skipped (Opted Out)");
                return Result.Success(string.Empty, "Skip: Opted Out");
            }

            if (converted)
            {
                _logger.LogInformation("Skip exporting {LeadId} to Lumin as it has already been converted", lead.Id);
                await AddIntegrationAsync(lead, "Skipped (Converted)");
                return Result.Success(string.Empty, "Skip: Converted");
            }

            _logger.LogInformation("First Time");
        }
        else
        {
            var luminIntegration = integrations.Where(x => x.IntegrationId == IntegrationIds.Lumin)
                .OfType<LuminLeadIntegration>()
                .FirstOrDefault();

            if (luminIntegration == null)
            {
                _logger.LogError("Lumin integration not of right type");
            }
            else if (luminIntegration.CancelledOn.HasValue)
            {
                _logger.LogInformation("Integration has been {CancelledOn}, skip sending updates", luminIntegration.CancelledOn);
                return Result.Success(string.Empty, "Ignore (Cancelled)");
            }

            if (doNotContact)
            {
                _logger.LogInformation("Opt out: DNC");
                action = Action.OptedOut;
            }
            else if (converted)
            {
                if (nextAppt?.Tool == "Lumin")
                {
                    _logger.LogInformation("Ignore Converted event since the appt was booked by lumin");
                    return Result.Success(string.Empty, "Ignore (Converted by Lumin)");
                }

                _logger.LogInformation("Opt out: Converted");
                action = Action.Converted;
            }
            else
            {
                _logger.LogInformation("Ignore update, no action to report for {LeadId}", lead.Id);
                return Result.Success(string.Empty, "Ignore (No meaningful change)");
            }
        }

        _logger.LogInformation("Get {OrganizationId}", lead.EntityId);

        var organization = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.Id, lead.EntityId)
            .FirstOrDefaultAsync();

        if (organization == null)
        {
            _logger.LogError("Failed to get {OrganizationId} for {LeadId}", lead.EntityId, lead.Id);
            await AddIntegrationAsync(lead, "Error (failed to load Organization)");
            return Result.Error<string>("Failed to get organization for lead");
        }

        var inspirenetBranchId = organization?.Identities.FirstOrDefault(x => x.IdentityProviderId == ExternalProvider.InspireNet.ToString())?.ExternalId;
        if (string.IsNullOrEmpty(inspirenetBranchId))
        {
            _logger.LogError("No InspireNet identity for {OrganizationId} for {LeadId}", lead.EntityId, lead.Id);
            await AddIntegrationAsync(lead, "Error (no InspireNet Id)");
            return Result.Error<string>("Couldn't figure out InspireNet identity for organization");
        }

        if (organization.Properties != null && organization.Properties.TryGetValue("LuminOptOut", out var luminOptOutObj) && (luminOptOutObj is bool luminOptOut) && luminOptOut)
        {
            _logger.LogInformation("Ignore {LeadId}, {OrganizationId} has opted out", lead.Id, organization.Id);
            await AddIntegrationAsync(lead, "Skipped (Organization Opted Out)");
            return Result.Success(string.Empty, "Skip: Organization Opted Out");
        }

        var leadSourceCode = lead[Lead.PropertyName_LeadSource] ?? lead[Lead.PropertyName_HowDidYouHearAboutUs];

        _logger.LogInformation("Export to lumin: {LeadSource}", leadSourceCode);

        var leadSource = await _connection.Filter<CustomObject>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.ObjectType, "SfLeadSource")
            .Eq(x => x.ExternalId, leadSourceCode)
            .FirstOrDefaultAsync();

        var payload = new Payload
        {
            Name = lead.Name,
            FirstName = lead.FirstName,
            LastName = lead.LastName,
            PrimaryPhone = lead.Phone,
            SecondaryPhone = lead["phone2"],
            LeadId = lead.Id,
            ReferrerCode = leadSource?.ExternalId,
            Referrer = leadSource?.Name,

            Address = lead.Address,
            City = lead.City,
            StateProvince = lead.State,
            PostalCode = lead.PostalCode,
            Email = lead.Email,

            SpEntityId = lead.EntityId.ToString(), // should be the Org 
            InspireNetBranchId = inspirenetBranchId,

            DoNotContact = doNotContact,
            Converted = converted,
            Authorization = CreateAuthorizationToken(lead, 180),
            Action = action,

            Host = _config.Host,
        };

        var fake = lead["luminFake"];
        if (!string.IsNullOrEmpty(fake) && bool.TryParse(fake, out var result))
        {
            payload.Fake = result;
        }

        var resp = await PublishAsync(payload);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Error exporting lead {StatusCode}: {Body}", resp.StatusCode, body);
            if (!alreadyExported)
            {
                await AddIntegrationAsync(lead, $"Request Failed ({resp.StatusCode})");
            }

            return Result.Error<string>($"Request Failed ({resp.StatusCode})");
        }

        try
        {
            var response = JsonConvert.DeserializeObject<Response>(body);
            if (response.Ok)
            {
                _logger.LogInformation("Lead {LeadId} successfully exported with {BotNumber}: {Body}", lead.Id, response.BotNumber, body);
                if (!alreadyExported)
                {
                    await AddIntegrationAsync(lead, "Exported", response.BotNumber);
                }
                else
                {
                    switch (action)
                    {
                        case Action.Converted:
                            await SetCancelledOnAsync(lead, "Cancelled (Converted)");
                            break;

                        case Action.OptedOut:
                            await SetCancelledOnAsync(lead, "Cancelled (Opt Out)");
                            break;
                    }
                }

                if (payload.Action == Action.OptedOut)
                {
                    _logger.LogInformation("Send Dead Lead Vendor");
                    payload.Action = Action.DeadLeadVendor;
                    try
                    {
                        var resp2 = await PublishAsync(payload);
                        var body2 = await resp2.Content.ReadAsStringAsync();
                        _logger.LogInformation("Received: {Body}", body2);
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogError(ex2, "Failed to send dead lead vendor");
                    }
                }

                return Result.Success(response.BotNumber, alreadyExported ? "Updated" : "Exported");
            }

            _logger.LogError("Failed to export Lead with {Code}({Message}): {Body}", response.ErrorCode, response.ErrorMessage, body);
            if (!alreadyExported)
            {
                await AddIntegrationAsync(lead, $"Failed: {response.ErrorCode}");
            }

            return Result.Error<string>($"Failed: {response.ErrorCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse response: {Response}", body);

            if (!alreadyExported)
            {
                await AddIntegrationAsync(lead, "Failed to parse response");
            }

            return Result.Error<string>("Failed to parse response");
        }
    }

    private async Task<Lead> AddIntegrationAsync(Lead lead, string status, string externalId = null)
    {
        lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.Id, lead.Id)
            .Update
            .Push(x => x.Integrations, new LuminLeadIntegration
            {
                IntegrationId = IntegrationIds.Lumin,
                ExternalId = externalId,
                CreatedOn = DateTime.UtcNow,
                Status = status,
            })
            // .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            // .Set(x => x.LastActor, context.Actor())                    
            .UpdateAndGetOneAsync();

        return lead;
    }

    public Task<HttpResponseMessage> PublishAsync(Payload payload)
    {
        var url = _config.Url;
        _logger.LogInformation("Post {Url}: {Body}", url, JsonConvert.SerializeObject(payload, SerializationSettings.Default));

        var json = JsonConvert.SerializeObject(payload, SerializationSettings.Default);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        return Client.PostAsync(url, httpContent);
    }

    public class Config
    {
        public string Key { get; set; }
        public string Url { get; set; }
        public string Host { get; set; }
    }
}

public class Payload : IMessageBody
{
    public string City { get; set; }
    public string Name { get; set; }
    public string FirstName { get; set; }

    [JsonProperty("lead_id")] public Guid LeadId { get; set; }

    public string SpEntityId { get; set; }
    public string StateProvince { get; set; }
    public string Address { get; set; }
    public string PrimaryPhone { get; set; }
    public string SecondaryPhone { get; set; }
    public string LastName { get; set; }
    public string PostalCode { get; set; }
    public string Email { get; set; }

    public bool Fake { get; set; }

    [JsonProperty("referrer_code")] public string ReferrerCode { get; set; }

    public string Referrer { get; set; }

    [JsonProperty("host_id_ext")] public string InspireNetBranchId { get; set; }

    public bool Converted { get; set; }
    public bool DoNotContact { get; set; }
    public string Authorization { get; set; }

    public string Action { get; set; }

    public string Host { get; set; }
}

public class Response
{
    [JsonProperty("ok")] public bool Ok { get; set; }

    [JsonProperty("bot_number")] public string BotNumber { get; set; }

    [JsonProperty("error_code")] public string ErrorCode { get; set; }

    [JsonProperty("error_msg")] public string ErrorMessage { get; set; }

    [JsonProperty("text")] public string Text { get; set; }
}