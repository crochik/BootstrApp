using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Intuit.Ipp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using PI.QuickBooks.Models;
using PI.QuickBooks.Services;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Salesforce.Models;

namespace Controllers;

[Produces("application/json")]
[Route("/quickbooks/v1/[controller]")]
public class InvoiceController : APIController
{
    private readonly MongoConnection _connection;
    private readonly QuickBooksService _service;

    public InvoiceController(MongoConnection connection, QuickBooksService service)
    {
        _connection = connection;
        _service = service;
    }

    [Authorize("admin")]
    [HttpGet("Organization({entityId})")]
    public async Task<IEnumerable<Invoice>> GetAsync([FromRoute] Guid entityId)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        return await _service.GetAllAsync<Invoice>(entity.Context);
    }

    [Authorize("admin")]
    [HttpPost("Option({externalId})")]
    public async Task<IEnumerable<string>> AddAsync([FromRoute] string externalId, [FromServices] QbInvoiceFromSalesforceExporter exporter)
    {
        var option = await _connection.Filter<SalesforceObject<SfOption>>("salesforce.INET_Option__c")
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.ExternalId, externalId)
            .FirstOrDefaultAsync();

        if (option == null) throw new NotFoundException($"Proposal not found: {externalId}");

        var result = await exporter.ExportSalesforceInvoiceAsync(Context, option.Id);
        if (!result.IsSuccess)
        {
            throw new BadRequestException(result.Status);
        }

        return exporter.Errors;
    }

    [Authorize("admin")]
    [HttpPost("Estimate({id})")]
    public async Task<IEnumerable<string>> AddAsync([FromRoute] Guid id, [FromServices] QbInvoiceFromEstimateExporter exporter)
    {
        var estimate = await _connection.Filter<PI.ProductCatalog.Models.Estimate>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (estimate == null) throw new NotFoundException($"Proposal not found: {id}");

        var result = await exporter.ExportInvoiceAsync(Context, estimate);
        if (!result.IsSuccess)
        {
            throw new BadRequestException(result.Status);
        }

        return exporter.Errors;
    }
}