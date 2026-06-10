using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Models;
using Services;

namespace Controllers.App;

[Route("/salesforce/app/[controller]")]
public class InspirenetController : APIController
{
    private const string WorkTypeName = "In Home Consultation";

    private readonly MongoConnection _connection;
    private readonly SalesforceLeadService _leadService;

    public InspirenetController(MongoConnection connection, SalesforceLeadService leadService)
    {
        _connection = connection;
        _leadService = leadService;
    }

    /// <summary>
    /// Create Lead
    /// </summary>
    [Authorize("default")]
    [HttpPost("Lead")]
    public async Task<CreateLeadResponse> CreateLeadAsync([FromBody] CreateLeadRequest request)
    {
        var lead = await CreateSalesforceLeadAsync(request);
        if (!lead.IsSuccess)
        {
            return new CreateLeadResponse
            {
                Error = lead.Status,
            };
        }

        return new CreateLeadResponse
        {
            LeadId = lead.Value,
        };
    }

    /// <summary>
    /// Create lead and convert
    /// </summary>
    [Authorize("default")]
    [HttpPost("Account")]
    public async Task<CreateAccountResponse> CreateAccountAsync([FromBody] CreateAccountRequest request)
    {
        var organization = await GetOrganizationAsync(request);

        var userId = Context.Role switch
        {
            EntityRoleId.Manager or EntityRoleId.User => Context.UserId,
            EntityRoleId.Admin => request.UserId,
            _ => null,
        };

        if (!userId.HasValue)
        {
            return new CreateAccountResponse
            {
                Error = "Missing User",
            };
        }

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.OrganizationId, organization.Id)
            .Eq(x => x.Id, userId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return new CreateAccountResponse
            {
                Error = "User not found",
            };
        }

        var sfUserId = user.Identities?
            .FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Salesforce) && x.Data != null)?
            .ExternalId;

        var sfServiceMemberId = user.Identities?
            .FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Salesforce) && x.Name == "ServiceResource")?
            .ExternalId;

        var lead = await CreateSalesforceLeadAsync(organization, request);
        if (!lead.IsSuccess)
        {
            return new CreateAccountResponse
            {
                Error = lead.Status,
            };
        }

        var serviceTerritoryId = organization.Identities.FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Salesforce))?.ExternalId;

        var body = new SalesforceLeadService.ConvertLeadRequest
        {
            LeadId = lead.Value,
            OwnerId = sfUserId,
            ServiceTerritoryId = serviceTerritoryId,
            ServiceMemberId = sfServiceMemberId,
            WorkTypeName = WorkTypeName,
            LeadSource = request.LeadSource,
        };

        var response = await _leadService.ConvertLeadAsync(Context, body);
        if (!response.IsSuccess)
        {
            return new CreateAccountResponse
            {
                Error = response.Status,
            };
        }

        return new CreateAccountResponse
        {
            LeadId = response.Value.LeadId,
            AccountId = response.Value.AccountId,
            WorkOrderId = response.Value.WorkOrderId,
            WorkOrderLineId = response.Value.WorkOrderLineId,
        };
    }

    private async Task<Result<string>> CreateSalesforceLeadAsync(CreateLeadRequest request)
    {
        var organization = await GetOrganizationAsync(request);
        return await CreateSalesforceLeadAsync(organization, request);
    }

    private async Task<Organization> GetOrganizationAsync(CreateLeadRequest request)
    {
        var organizationId = Context.Role switch
        {
            EntityRoleId.Manager or EntityRoleId.User => Context.OrganizationId,
            EntityRoleId.Admin => request.OrganizationId,
            _ => null,
        };

        if (!organizationId.HasValue) return null;

        var organization = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, organizationId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        return organization;
    }

    private async Task<Result<string>> CreateSalesforceLeadAsync(Organization organization, CreateLeadRequest request)
    {
        if (organization == null) return Result.Error<string>("Organization not found");
        var serviceTerritoryId = organization.Identities.FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.InspireNet))?.ExternalId;
        if (string.IsNullOrWhiteSpace(serviceTerritoryId)) return Result<string>.Error("Missing Service Territory");

        var dict = new Dictionary<string, object>
        {
            { nameof(CreateLeadRequest.FirstName), request.FirstName },
            { nameof(CreateLeadRequest.LastName), request.LastName },
            { "company", $"{request.FirstName} {request.LastName}".Trim() },
            { nameof(CreateLeadRequest.Phone), SalesforceLeadService.GetPlainPhoneNumber(request.Phone) },
            { "street", request.Address },
            { nameof(CreateLeadRequest.City), request.City },
            { nameof(CreateLeadRequest.State), request.State },
            { nameof(CreateLeadRequest.PostalCode), request.PostalCode },
            { nameof(CreateLeadRequest.Country), request.Country },
            { nameof(CreateLeadRequest.Email), request.Email },
            { "INET_SELECTION_NOTES__c", request.Notes },
            { "External_Lead__c", false },
            {
                "INET_Service_Territory__r", new
                {
                    Branch_Code__c = serviceTerritoryId,
                }
            },
            { nameof(CreateLeadRequest.LeadSource), request.LeadSource },
            // PIId__c
        };

        return await _leadService.ExportLeadToSalesforceAsync(Context, dict);
    }
}

public class CreateLeadRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Address { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }
    public string Notes { get; set; }

    public Guid? OrganizationId { get; set; }

    public string LeadSource { get; set; }
}

public class CreateAccountRequest : CreateLeadRequest
{
    public Guid? UserId { get; set; }
}

public class CreateLeadResponse
{
    public string LeadId { get; set; }
    public string Error { get; set; }
}

public class CreateAccountResponse : CreateLeadResponse
{
    public string AccountId { get; set; }
    public string WorkOrderId { get; set; }
    public string WorkOrderLineId { get; set; }
}