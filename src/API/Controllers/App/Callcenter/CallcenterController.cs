using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Layout;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers.Callcenter;

[Authorize("default")] // should it have its own access rules?
[ApiExplorerSettings(GroupName = "callcenter")]
[Produces("application/json")]
[Route("/app/[controller]")]
public class CallcenterController : APIController
{
    private static string[] SfObjects = { "Account", "Lead" };

    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly IMessageBroker _messageBroker;

    // TODO: should get it from database/account 
    // ...
    private readonly CallcenterConfiguration _configuration;

    public CallcenterController(
        IConfiguration configuration,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        IMessageBroker messageBroker
    )
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
        _messageBroker = messageBroker;
        _configuration = configuration.GetSection(nameof(CallcenterController)).Get<CallcenterConfiguration>();
    }

    [HttpPost("ZeeFinder/DataForm")]
    public async Task<DataFormActionResponse> ZeeFinderFormOnActionAsync([FromBody] DataFormActionRequest request)
    {
        if (!request.Parameters.TryGetStrParam("PostalCode", out var postalCode))
        {
            return new DataFormActionResponse(request, "Missing postal code");
        }

        // TODO: use object information instead of hard coding 
        // ...
        var match = await _connection.Filter<CustomObject>("CustomObject")
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.ObjectType, "ZeeTerritory")
            .Eq(x => x.ExternalId, postalCode)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (match == null) return new DataFormActionResponse(request, $"No Territory found for {postalCode}");

        return new DataFormActionResponse(request)
        {
            NextUrl = $"page:/Organization?id={match.EntityId}",
            Success = true,
        };
    }

    [HttpPost("IncomingCall/DataForm")]
    public async Task<DataFormActionResponse> EditFormOnActionAsync([FromBody] DataFormActionRequest request)
    {
        var organizationId = default(Guid?);
        if (request.Parameters.TryGetGuidParam("OrganizationId", out var entityId))
        {
            // return new DataFormActionResponse(request, "Missing organization");
            organizationId = entityId;
        }

        request.Parameters.TryGetStrParam("Phone", out var phoneNumber);
        request.Parameters.TryGetStrParam("FullTextSearch", out var fullTextSearch);

        if (string.IsNullOrWhiteSpace(phoneNumber) && string.IsNullOrWhiteSpace(fullTextSearch))
        {
            // neither, error
            return new DataFormActionResponse(request, "Please provide phone number and/or full name");
        }

        if (organizationId.HasValue)
        {
            // check it is a valid organization
            var organization = await _connection.Filter<Entity, Organization>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, entityId)
                .Eq(x => x.IsActive, true)
                .FirstOrDefaultAsync();

            if (organization == null)
            {
                return new DataFormActionResponse(request, "Invalid organization");
            }
        }

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.Id, Context.UserId.Value)
            .FirstOrDefaultAsync();

        var leadQuery = _connection.Filter<Lead>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Ne(x => x.IsActive, false)
            .SortDesc(x => x.CreatedOn)
            .Limit(11);

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            leadQuery.Eq(x => x.NormalizedPhoneNumber, Lead.GetNormalizedPhoneNumber(phoneNumber));
        }

        if (!string.IsNullOrWhiteSpace(fullTextSearch))
        {
            leadQuery.Text(fullTextSearch[0] == '"' ? fullTextSearch : $"\"{fullTextSearch}\"");
        }

        if (organizationId.HasValue)
        {
            leadQuery.Eq(x => x.EntityId, organizationId);
        }

        // try to find locally
        var list = await leadQuery.FindAsync();

        if (list.Count > 1)
        {
            return new DataFormActionResponse(request, "Multiple matches")
            {
                NextUrl = $"dataGrid:/api/v1/CustomObject/Lead?{string.Join('&', queryParams())}",
                Success = true,
            };

            IEnumerable<string> queryParams()
            {
                if (organizationId.HasValue) yield return $"EntityId={organizationId.Value}";
                if (!string.IsNullOrWhiteSpace(fullTextSearch)) yield return $"{Uri.EscapeDataString(Condition.FullTextSearch)}={Uri.EscapeDataString(fullTextSearch)}";
                if (!string.IsNullOrWhiteSpace(phoneNumber)) yield return $"NormalizedPhoneNumber={phoneNumber}";
            }
        }

        if (list.Count == 1)
        {
            var lead = list[0];
            await fireLeadLoadedEventAsync(lead);

            // if embedded ... 
            // look for salesforce integrations
            // var nextUrl = $"page://Lead?id={lead.Id}&Id={lead.Id}&OrganizationId={organizationId ?? lead.EntityId}";
            var nextUrl = $"page://Scheduler?LeadId={lead.Id}&OrganizationId={organizationId ?? lead.EntityId}";

            var salesforceIntegrations = lead.Integrations?.Where(x => x.IntegrationId == IntegrationIds.Salesforce).DistinctBy(x => x.Tag).ToDictionary(x => x.Tag);
            if (salesforceIntegrations?.Count > 0)
            {
                foreach (var obj in SfObjects)
                {
                    if (salesforceIntegrations.TryGetValue(obj, out var sfObject))
                    {
                        nextUrl = await buildSalesforceUrlAsync(sfObject.ExternalId);
                        break;
                    }
                }
            }

            return new DataFormActionResponse(request)
            {
                NextUrl = nextUrl,
                Success = true,
            };
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber) && string.IsNullOrWhiteSpace(fullTextSearch))
        {
            // fallback to salesforce
            var digits = DigitsOnly(phoneNumber);

            // salesforce org, try to look into salesforce objects 
            foreach (var obj in SfObjects)
            {
                // TODO: should it exclude deleted/deactivated?
                // ...

                // hack!!!!
                // TODO: should use object type so will not have to hard code collection and database name
                // ...
                var sfObject = await _connection.Filter<SalesforceCustomObject>($"salesforce.{obj}")
                    .Eq(x => x.AccountId, Context.AccountId)
                    // .Eq(x => x.EntityId, organization.Id)
                    .Eq(x => x.Properties["Phone"], digits)
                    .SortDesc(x => x.Properties["CreatedDate"])
                    .FirstOrDefaultAsync();

                if (sfObject != null)
                {
                    await fireLeadLoadedEventAsync(sfObject);

                    return new DataFormActionResponse(request)
                    {
                        NextUrl = await buildSalesforceUrlAsync(sfObject.ExternalId),
                        Success = true,
                    };
                }
            }
        }

        if (!organizationId.HasValue)
        {
            return new DataFormActionResponse(request, "No Lead found and couldn't determine the Organization.", false);
        }

        return new DataFormActionResponse(request)
        {
            // NextUrl = $"dataForm://api/v1/CustomObject/Lead?NormalizedPhoneNumber={phoneNumber}&EntityId={organization.Id}",
            // NextUrl = $"dataForm://app/Callcenter/Organization({organization.Id})/Lead?normalizedPhoneNumber={phoneNumber}",
            NextUrl = $"page://app/Callcenter/Organization({organizationId})/Lead?{string.Join('&', queryParameters())}",
            Success = true,
        };

        ValueTask<string> buildSalesforceUrlAsync(string externalId)
        {
            return ValueTask.FromResult($"{_configuration.InstanceUrl}/{externalId}");
        }

        IEnumerable<string> queryParameters()
        {
            if (!string.IsNullOrWhiteSpace(phoneNumber)) yield return $"normalizedPhoneNumber={phoneNumber}";
            if (!string.IsNullOrWhiteSpace(fullTextSearch)) yield return $"name={Uri.EscapeDataString(fullTextSearch)}";
        }

        async Task fireLeadLoadedEventAsync(IFlowObject obj)
        {
            var evt = new GenericFlowEvent(obj)
            {
                Actor = Context.Actor(),
                Action = "CallcenterLoad",
                Description = $"{user?.Name ?? "UNKNOWN"} loaded {obj.Name} ({obj.ObjectType})",
                EventTypeId = EventIds.OnObjectLoaded,
            };

            evt.AddRefValue(obj.ObjectType, obj.Id);
            if (organizationId.HasValue)
            {
                evt.AddRefValue(nameof(Organization), organizationId);
            }

            if (user != null) evt.AddRefValue(user);

            await _messageBroker.DispatchAsync(evt);
        }
    }

    /// <summary>
    /// Get Page for object 
    /// </summary>
    [HttpGet("Organization({organizationId})/Lead/DataPage")]
    public LayoutPage GetObjectPageAsync([FromRoute] Guid organizationId, [FromQuery] string normalizedPhoneNumber, [FromQuery] string name)
    {
        var page = new LayoutPage
        {
            Name = "NewLead",
            Label = "New Lead",
            Layout = new LayoutContainer
            {
                Type = LayoutContainerType.Row,
                Spacing = 12,
                Justify = LayoutJustify.Between,
                Children = new[]
                {
                    new ObjectLayoutItem
                    {
                        Url = new Uri($"dataForm://app/Callcenter/Organization({organizationId})/Lead?{string.Join('&', queryParameters())}").ToString(),
                        Weight = 1,
                    },
                    new ObjectLayoutItem
                    {
                        Url = new Uri($"dataForm://api/v1/CustomObject/Organization({organizationId})/details").ToString(),
                        Weight = 1,
                    },
                },
            },
            Menu = new Menu
            {
                Name = "Menu",
                Items =
                [
                    new ActionMenuItem
                    {
                        Name = "Home",
                        Label = "Home",
                        Action = "page://Callcenter",
                    }
                ]
            }
        };

        return page;

        IEnumerable<string> queryParameters()
        {
            if (!string.IsNullOrWhiteSpace(normalizedPhoneNumber)) yield return $"normalizedPhoneNumber={normalizedPhoneNumber}";
            if (!string.IsNullOrWhiteSpace(name)) yield return $"name={Uri.EscapeDataString(name)}";
        }
    }

    [HttpGet("Organization({organizationId})/Lead/DataForm")]
    public async Task<Form> GetAddFormAsync([FromRoute] Guid organizationId, [FromQuery] string normalizedPhoneNumber, [FromQuery] string name)
    {
        var result = await _objectTypeService.GetAddDataFormAsync(Context, nameof(Lead));
        if (result == null) throw new NotFoundException();

        // init 
        var fields = result.Fields.ToDictionary(x => x.Name);
        if (!string.IsNullOrWhiteSpace(normalizedPhoneNumber)) setFieldValue(nameof(Lead.NormalizedPhoneNumber), normalizedPhoneNumber);
        if (!string.IsNullOrWhiteSpace(name)) setFieldValue(nameof(Lead.Name), name);
        setFieldValue(nameof(Lead.EntityId), organizationId, true);
        setFieldValue(nameof(Lead.LeadTypeId), LeadTypeIds.Callcenter, true);

        return result;

        void setFieldValue(string name, object value, bool disable = false)
        {
            if (!fields.TryGetValue(name, out var field) || field.DefaultValue != null) return;
            field.DefaultValue = field.AutoConvert(value);
            if (disable)
            {
                field.Enable = new[] { "false" };
                field.Visible = new[] { "false" };
            }
        }
    }

    [HttpPost("Organization({organizationId})/Lead/DataForm")]
    public async Task<DataFormActionResponse> EditFormOnActionAsync([FromRoute] Guid organizationId, [FromBody] DataFormActionRequest request)
    {
        if (request.Action != FormAction.Add) throw new ForbiddenException();

        // TODO: valida fields?
        // ...

        var result = await _objectTypeService.ExecObjectActionAsync(Context, nameof(Lead), request);
        if (result == null) throw new NotFoundException();

        if (!result.Success) return result;

        // TODO: redirect to ... 
        // ...
        return new DataFormActionResponse(request)
        {
            NextUrl = $"page://Scheduler?OrganizationId={organizationId}&LeadId={result.Ids[0]}",
            Success = true,
        };
    }

    /// <summary>
    /// hack to convert phone number into sf digits only 
    /// </summary>
    private string DigitsOnly(string phoneNumber)
    {
        if (phoneNumber.StartsWith("+1")) phoneNumber = phoneNumber[2..];
        var result = "";
        foreach (var chr in phoneNumber)
        {
            if (chr >= '0' && chr <= '9') result += chr;
        }

        return result;
    }
}

/// <summary>
/// Configuration for the callcenter
/// TODO: move into database/account integration so we can support multiple tenants
/// ...
/// </summary>
public class CallcenterConfiguration
{
    public string InstanceUrl { get; set; }
}