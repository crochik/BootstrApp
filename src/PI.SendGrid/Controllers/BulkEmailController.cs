using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Models;
using Services;

namespace Controllers;

[Route("/sendgrid/v1/[controller]")]
// [Authorize("admin")]
[AllowAnonymous]
public class BulkEmailController : APIController
{
    [HttpGet]
    public async Task ProcessAsync([FromServices] BulkEmailService service)
    {
        await service.QueueNextAsync(new AccountContext(AccountIds.FCI));
    }
}