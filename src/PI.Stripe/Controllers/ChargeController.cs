using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using Services;

namespace Controllers;

[Authorize("admin")]
[Produces("application/json")]
[Route("/stripe/v1/[controller]")]
public class ChargeController : APIController
{
    private readonly StripeService _service;
    private readonly IOrganizationAdapter _organizationAdapter;

    public ChargeController(
        StripeService service,
        IOrganizationAdapter organizationAdapter
    )
    {
        _service = service;
        _organizationAdapter = organizationAdapter;
    }

    [HttpPost("Sync")]
    [ProducesResponseType(typeof(bool), 200)]
    public async Task<IActionResult> GetChargesAsync()
    {
        var hasMore = await _service.SyncCharges(Context);
        return Ok(hasMore);
    }
    
    [Authorize("admin")]
    [HttpPost("/stripe/v1/Organization({id})/[controller]")]
    [ProducesResponseType(typeof(ChargeResponse), 200)]
    public async Task<IActionResult> AddChargeUsingPaymentIntentAsync([FromRoute] Guid id, decimal value)
    {
        var organization = await _organizationAdapter.GetByIdAsync(Context, id);
        if (organization == null) return NotFound();

        var result = await _service.AddChargeUsingPaymentAsync(Context, organization, "Test", value);
        var response = new ChargeResponse
        {
            Id = result.Value?.Id,
            Status = result.Status,
            IsSuccess = result.IsSuccess
        };

        return Ok(response);
    }    

    public class ChargeResponse
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public bool IsSuccess { get; set; }
    }
}