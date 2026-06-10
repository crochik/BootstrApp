using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Intuit.Ipp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.QuickBooks.Services;
using PI.Shared.Controllers;
using PI.Shared.Models;

namespace Controllers;

[Produces("application/json")]
[Route("/quickbooks/v1/[controller]")]
public class CompanyController : APIController
{
    private readonly MongoConnection _connection;
    private readonly QuickBooksService _service;

    public CompanyController(MongoConnection connection, QuickBooksService service)
    {
        _connection = connection;
        _service = service;
    }

    [Authorize("admin")]
    [HttpGet("Organization({entityId})")]
    public async Task<CompanyInfo> GetAsync([FromRoute] Guid entityId)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        return await _service.GetCompanyInfoAsync(entity.Context);
    }

    
    [Authorize("admin")]
    [HttpGet("Organization({entityId})/Preferences")]
    public async Task<Preferences> GetPreferencesAsync([FromRoute] Guid entityId)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var preferences = (await _service.GetAllAsync<Preferences>(entity.Context)).FirstOrDefault();

        var autoTax = preferences?.TaxPrefs.PartnerTaxEnabled ?? false;
        
        return preferences;
    }

    [Authorize("manager")]
    [HttpGet]
    public async Task<CompanyInfo> GetAsync()
    {
        return await _service.GetCompanyInfoAsync(Context);
    }
}