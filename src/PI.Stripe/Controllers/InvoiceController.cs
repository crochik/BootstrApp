using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using Services;

namespace Controllers;

[Authorize("admin")]
[Produces("application/json")]
[Route("/stripe/v1/[controller]")]
public class InvoiceController : APIController
{
    private readonly StripeService _service;

    public InvoiceController(StripeService service)
    {
        _service = service;
    }

    [HttpPost("Sync")]
    [ProducesResponseType(typeof(bool), 200)]
    public async Task<IActionResult> GetInvoicesAsync()
    {
        var hasMore = await _service.SyncInvoices(Context);
        return Ok(hasMore);
    }        
}