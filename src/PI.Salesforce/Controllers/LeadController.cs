using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetCoreForce.Models;
using Newtonsoft.Json;
using PI.Shared.Controllers;
using PI.Shared.Services;

namespace Controllers;

[Route("/salesforce/v1/[controller]")]
public class LeadController : APIController
{
    private readonly IMapper _mapper;
    private readonly SalesforceService _salesforceService;

    public LeadController(
        IMapper mapper,
        SalesforceService salesforceService
    )
    {
        _mapper = mapper;
        _salesforceService = salesforceService;
    }

    // [Authorize("admin")]
    // [HttpPost("/salesforce/v1/[controller]")]
    // // [ProducesResponseType(typeof(ObjectMetaData), 200)]
    // public async Task<IActionResult> AddLeadAsync(
    //     [FromBody] ExportToSalesforceAction.Message lead,
    //     [FromServices] ISalesforceLeadService leadService)
    // {
    //     if (lead == null) return BadRequest();

    //     // generate random id
    //     lead.Lead.Id = Guid.NewGuid();

    //     var created = await leadService.CreateAsync(lead.Lead.EntityId, lead);

    //     return Ok(created);
    // }

    [Authorize("partner")]
    [HttpGet("/salesforce/v1/[controller]({id})")]
    [ProducesResponseType(typeof(Lead), 200)]
    public async Task<IActionResult> GetAsync([FromRoute] string id)
    {
        var fields = new[]
        {
            "Id",
            "Name",
            "Call_Instructions__c",
            "DoNotCall",
            "IsConverted",
            "ConvertedAccountId",
            "ConvertedContactId",
            "ConvertedOpportunityId",
            "Phone",
            "MobilePhone",
            "Status",
            "LeadSource",
            "LastModifiedDate",
            "PostalCode"
        };

        var result = await _salesforceService.QueryByIdAsync<dynamic>(Context, SfLead.SObjectTypeName, id, fields);
        return result != null ? Ok(_mapper.Map<Lead>(result)) : NotFound();
    }

    private class Lead
    {
        public string Id { get; set; }

        public string Name { get; set; }

        [JsonProperty("callInstructions")] public string Call_Instructions__c { get; set; }
        public bool? DoNotCall { get; set; }
        public bool IsConverted { get; set; }
        public string ConvertedAccountId { get; set; }
        public string ConvertedContactId { get; set; }
        public string ConvertedOpportunityId { get; set; }
        public string Phone { get; set; }
        public string MobilePhone { get; set; }
        public string Status { get; set; }
        public string LeadSource { get; set; }
        public DateTime? LastModifiedDate { get; set; }
        public string PostalCode { get; set; }
    }
}