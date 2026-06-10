using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Intuit.Ipp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.QuickBooks.Models;
using PI.QuickBooks.Services;
using PI.Shared.Controllers;
using PI.Shared.Models;

namespace Controllers;

[Produces("application/json")]
[Route("/quickbooks/v1/[controller]")]
public class VendorController : APIController
{
    private readonly MongoConnection _connection;
    private readonly QuickBooksService _service;

    public VendorController(MongoConnection connection, QuickBooksService service)
    {
        _connection = connection;
        _service = service;
    }

    [Authorize("admin")]
    [HttpGet("Organization({entityId})")]
    public async Task<IEnumerable<Vendor>> GetAsync([FromRoute] Guid entityId)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        return await _service.GetAllAsync<Vendor>(entity.Context);
    }

    [Authorize("admin")]
    [HttpPost("Organization({entityId})")]
    public async Task<IEnumerable<QbEntity>> AddVendorsAsync([FromRoute] Guid entityId)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var context = entity.Context.WithActorFrom(Context);

        return await _service.ExportVendorsAsync(context);
    }
}