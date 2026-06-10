using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Intuit.Ipp.Data;
using Intuit.Ipp.DataService;
using Intuit.Ipp.QueryFilter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization.Attributes;
using PI.QuickBooks.Models;
using PI.QuickBooks.Services;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using EmailAddress = Intuit.Ipp.Data.EmailAddress;

namespace Controllers;

[Produces("application/json")]
[Route("/quickbooks/v1/[controller]")]
public class CustomerController : APIController
{
    private readonly MongoConnection _connection;
    private readonly QuickBooksService _service;

    public CustomerController(MongoConnection connection, QuickBooksService service)
    {
        _connection = connection;
        _service = service;
    }

    /// <summary>
    /// Test method to get customers ...it will not handle pagination 
    /// </summary>
    [Authorize("admin")]
    [HttpGet("Organization({entityId})")]
    public async Task<IEnumerable<Customer>> GetAsync([FromRoute] Guid entityId)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        return await _service.GetAllAsync<Customer>(entity.Context);
    }

    /// <summary>
    /// Test method to get customers ...it will not handle pagination 
    /// </summary>
    [Authorize("manager")]
    [HttpGet]
    public async Task<IEnumerable<Customer>> GetAsync()
    {
        return await _service.GetAllAsync<Customer>(Context);
    }

    // [Authorize("admin")]
    // [HttpPost("Lead({leadId})")]
    // public async Task<Result<QbEntity>> ExportLeadAsync([FromRoute] Guid leadId)
    // {
    //     var lead = await _connection.Filter<Lead>()
    //         .Eq(x => x.AccountId, Context.AccountId)
    //         .Eq(x => x.Id, leadId)
    //         .Ne(x => x.IsActive, false)
    //         .FirstOrDefaultAsync();
    //
    //     var entity = await _connection.Filter<Entity, Organization>()
    //         .Eq(x => x.AccountId, Context.AccountId)
    //         .Eq(x => x.Id, lead.EntityId)
    //         .Ne(x => x.IsActive, false)
    //         .FirstOrDefaultAsync();
    //
    //     // var projects = await _connection.Filter<SfWorkOrder>()
    //     //     .Eq(x => x.AccountId, lead.AccountId)
    //     //     .Eq(x => x.LeadId, lead.Id)
    //     //     .FindAsync();
    //
    //     return await _service.GetOrCreateAsync(entity.Context, lead);
    // }

    // private async Task<IResult> AddCustomerAsync(IEntityContext context, Lead lead, List<SfWorkOrder> projects)
    // {
    //     if (lead.Integrations.All(x => x.IntegrationId != IntegrationIds.QuickBooks))
    //     {
    //         var customerResult = await _service.GetOrCreateAsync(context, lead);
    //         if (!customerResult.IsSuccess)
    //         {
    //             throw new BadRequestException(customerResult.Status);
    //         }
    //     }
    //
    //     if (projects != null)
    //     {
    //         foreach (var project in projects)
    //         {
    //             if (!project.Integrations?.TryGetValue(nameof(IntegrationIds.QuickBooks), out var customerId) ?? true)
    //             {
    //                 var customerResult = await _service.AddAsync(context, project, lead);
    //                 if (!customerResult.IsSuccess)
    //                 {
    //                     throw new BadRequestException(customerResult.Status);
    //                 }
    //             }
    //             
    //             await _service.CreateEstimateAsync(context, lead, project, null);
    //         }
    //     }
    //
    //     return Result.Success("Exported");
    // }
}