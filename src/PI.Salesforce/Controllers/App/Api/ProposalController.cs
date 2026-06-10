using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.ProductCatalog.Models;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Route("/salesforce/api/[controller]")]
public class ProposalController(MongoConnection connection, SalesforceService service) : APIController
{
    [Authorize("admin")]
    [HttpPut("/salesforce/api/[controller]({externalId})/{status}")]
    public async Task<IActionResult> ChangeStatusAsync([FromRoute] string externalId, [FromRoute] SalesforceOptionStatus status)
    {
        var token = await service.GetTokenAsync(Context, GetTokenOptions.Default);
        if (token.IsError) return BadRequest("Can't get token");

        var sfObject = new Dictionary<string, object>
        {
            { "Option_Status__c", status.ToString() },
        };

        // Landed_Date__c
        // Install_Date__c
        // Produced_Date__c
        // Install_Date_Start__c
        // Install_Date_End__c
        // Product_Ordered__c


        // Installation_Status__c
        /*
        "Go Back Scheduled": "Go Back Scheduled",
        "Install Complete": "Install Complete",
        "Final Completed": "Final Completed",
        "Go Back Completed": "Go Back Completed",
        "Warranty scheduled": "Warranty scheduled",
        "Warranty Completed": "Warranty Completed",
        "Not Scheduled": "Not Scheduled",
        "In Progress": "In Progress",
        "Final Scheduled": "Final Scheduled"
        */

        await service.SalesforceClient.UpdateAsync(token.Value, "INET_Option__c", externalId, sfObject);
        return Ok();
    }

    [Authorize("admin")]
    [HttpPost("/salesforce/api/[controller]({id})")]
    public async Task<IActionResult> ExportProposalAsync([FromRoute] Guid id)
    {
        var estimate = await connection.Filter<Estimate>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        var entity = await connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, estimate.CreatedBy)
            .FirstOrDefaultAsync();

        var identity = entity?.Identities?.FirstOrDefault(x => x.IdentityProviderId == "Salesforce" && x.Data != null);
        var createdById = identity?.ExternalId;

        var token = await service.GetTokenAsync(Context, GetTokenOptions.Default);
        if (token.IsError) return BadRequest("Can't get token");

        var sfObject = new Dictionary<string, object>
        {
            { "Name", estimate.Name }, // proposal number? 
            { "Option_Number__c", estimate.EstimateNumber ?? 1.ToString() }, // proposal number? 
            { "Name__c", estimate.Description }, // ???
            { "ParentProject__c", estimate.ProjectExternalId },
            { "MobileGUID__c", estimate.Id.ToString() },
            { "TotalCost__c", estimate.TotalCost },
            { "TotalPrice__c", estimate.GrandTotal },
            { "TotalTax__c", estimate.GrandTax },

            // https://docs.google.com/document/d/1Dy4B0f4S3bLRcp66EIR6VK7hN08um2adme51MxbHtXQ/edit?usp=sharing
            // { "CreatedDate", DateTime.UtcNow },
            // { "CreatedById", createdById },

            // Option_Project_Proposal_Link__c

            // Branch__c
            // Project_Name__c
            // { "Customer_Name__c", estimate.TotalTax },
            // { "CustomerName__c", estimate.TotalTax },
            // { "Option_Status__c", estimate.TotalTax },
            // Design_Associate__c
            // Leading_Products__c
            // Notes__c
            // Product_Types__c

            // ro 
            // { "Total_Revenue__c", estimate.TotalPrice - estimate.TotalCost},
            // { "Proposal_Number__c", estimate.EstimateNumber ?? 1.ToString() }, // proposal number? 
            // { "TotalAfterDiscountBeforeTax__c", estimate.TotalPrice },
            // { "TotalBeforeTax__c", estimate.TotalPrice }, // ???
            // { "Total_price_before_tax__c", estimate.TotalPrice }, // ???
            // { "RollUpCost__c", estimate.TotalCost },
            // { "RollUpPrice__c", estimate.TotalPrice  },
            // { "RollUpTax__c", estimate.TotalTax },
            // { "LineItemRollUpCost__c", estimate.TotalCost },
            // { "LineItemRollUpPriceBeforeTax__c", estimate.TotalPrice  },
            // { "LineItemRollUpPrice__c", estimate.TotalPrice + estimate.TotalTax  },
            // { "LineItemRollUpTax__c", estimate.TotalTax },
            // { "SectionRollUpCost__c", estimate.TotalCost },
            // { "SectionRollUpPrice__c", estimate.TotalPrice  },
            // { "SectionRollUpTax__c", estimate.TotalTax },

            // Balanced_Amount__c

            // Adjustment__c

            // Custom_Section__c

            // Landed_Date__c
            // Install_Date__c
            // Produced_Date__c
            // Install_Date_Start__c
            // Install_Date_End__c
            // Product_Ordered__c

            // Installation_Status__c
            /*
            "Go Back Scheduled": "Go Back Scheduled",
            "Install Complete": "Install Complete",
            "Final Completed": "Final Completed",
            "Go Back Completed": "Go Back Completed",
            "Warranty scheduled": "Warranty scheduled",
            "Warranty Completed": "Warranty Completed",
            "Not Scheduled": "Not Scheduled",
            "In Progress": "In Progress",
            "Final Scheduled": "Final Scheduled"
            */


            // Client__c
            // DAInformation__c
            // LocationInformation__c
            // ProposalInformation__c
            // WhoIsFCI__c
            // ProposalInformationString__c
            // Proposal_Note__c

            // WasteFactor__c

            // AdditionalDiscountName__c
            // AdditionalDiscount__c
            // DiscountTax__c
            // ProductTaxGroup__c
            // Labor_Tax_Group_Id__c
            // Product_Tax_Group_Id__c

            // ReferenceListDiscount__c

            // DownPayment__c
            // NumberOfPayments__c
            // InterestRate__c
            // PricePaymentDuration__c
            // PricePaymentAppointment__c

            // IsManualEstimate__c
            // LaborTaxGroup__c
            // MobileGUID__c
            // { "RecordTypeId", "" },
            // LastActivityDate
            // LastViewedDate
            // LastReferencedDate
            // LastModifiedDate
            // LastModifiedById
            // SystemModstamp
        };

        var result = await service.SalesforceClient.CreateAsync(token.Value, "INET_Option__c", sfObject);
        return Ok(result);
    }

    public enum SalesforceOptionStatus
    {
        Produced,
        Paid,
        Dead,
        Canceled,
        Estimated,
        Landed,
        Scheduled,

        // "Landed in InspireNet": "Landed in InspireNet",
    }
}