using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;

namespace Controllers;

[Authorize("admin")]
[Route("/ime/v1/[controller]")]
[Produces("application/json")]
public class WorkOrderController : APIController
{
    private readonly IME.API.Client _client;

    public WorkOrderController(
        IME.API.Client client
        )
    {
        _client = client;
    }

    [HttpGet("/ime/v1/[controller]({id})")]
    public async Task<IActionResult> GetAsync(int id)
    {
        await _client.LoginAsync();
        var workOrder = await _client.WorkOrdersAsync(id);

        return Ok(workOrder);
    }
}