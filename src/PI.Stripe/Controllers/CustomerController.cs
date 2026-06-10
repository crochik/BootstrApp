using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using Services;

namespace Controllers;

[Produces("application/json")]
[Route("/stripe/v1/[controller]")]
public class CustomerController : APIController
{
    private readonly StripeService _service;
    private readonly IOrganizationAdapter _organizationAdapter;
    private readonly IUserAdapter _userAdapter;

    public CustomerController(
        StripeService service,
        IOrganizationAdapter organizationAdapter,
        IUserAdapter userAdapter
    )
    {
        _service = service;
        _organizationAdapter = organizationAdapter;
        _userAdapter = userAdapter;
    }

    [Authorize("manager")]
    [HttpPost]
    public async Task<IActionResult> AddCustomerAsync(string tokenId)
    {
        var organization = await _organizationAdapter.GetByIdAsync(Context, Context.OrganizationId.Value);
        var user = await _userAdapter.GetByIdAsync(Context, Context.UserId.Value);
        if (organization == null || user == null) return Forbid();
        if (string.IsNullOrEmpty(user.Email)) return BadRequest("No email defined for user");

        var result = await _service.AddCardToOrganizationAsync(Context, organization, user, tokenId);
        if (result == null) return BadRequest("Failed");

        return Ok();
    }

    [Authorize("admin")]
    [HttpPost("Sync")]
    [ProducesResponseType(typeof(bool), 200)]
    public async Task<IActionResult> GetCustomersAsync()
    {
        var hasMore = await _service.SyncCustomers(Context);
        return Ok(hasMore);
    }

    [Authorize("manager")]
    [HttpGet("Portal")]
    [ProducesResponseType(typeof(RedirectResponse), 200)]
    public async Task<IActionResult> RedirectToPortalAsync()
    {
        var returnUrl = Request.Headers.TryGetValue("Referer", out var headers) ? headers.First() : null;
        var url = await _service.GetPortalUrlAsync(Context, returnUrl);
        if (string.IsNullOrEmpty(url)) return NotFound();

        return Ok(new RedirectResponse
        {
            Url = url
        });
    }

    // [Authorize("manager")]
    // [HttpGet("Setup")]
    // public async Task<IActionResult> InitiateSetupAsync()
    // {
    //     var secret = await _service.InitiateSetupAsync(Context);
    //     return Ok(secret);
    // }
    //
    // [Authorize("admin")]
    // [HttpGet("/stripe/v1/Entity({id})/Payments")]
    // public async Task<IActionResult> GetPaymentsInfoAsync([FromRoute] Guid id)
    // {
    //     var user = await _connection.Filter<Entity>()
    //         .Eq(x => x.AccountId, Context.AccountId)
    //         .Eq(x => x.Id, id)
    //         .Ne(x => x.IsActive, false)
    //         .FirstOrDefaultAsync();
    //     
    //     var secret = await _service.GetPaymentMethodsAsync(user);
    //     return Ok(secret);
    // }

    public class RedirectResponse
    {
        public string Url { get; set; }
    }
}