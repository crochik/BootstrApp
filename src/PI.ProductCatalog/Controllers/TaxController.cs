using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.ProductCatalog.Services;
using PI.Shared.Controllers;

namespace Controllers;

[Route("/productcatalog/v1/[controller]")]
public class TaxController(TaxService service) : APIController
{
    [Authorize("admin")]
    [HttpGet("{postalCode}")]
    public async Task<IActionResult> TestAsync([FromRoute] string postalCode, [FromQuery] string state=null, [FromQuery] string city=null, [FromQuery] string address=null, [FromQuery] string country=null)
    {
        var result = await service.CalculateTaxLiability(postalCode, city, state, address, country);
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Status);
    }
}