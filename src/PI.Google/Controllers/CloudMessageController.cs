using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Google.Services;
using PI.Shared.Controllers;

namespace PI.Google.Controllers;

[Route("/google/v1/[controller]")]
public class MessageController : APIController
{
    private readonly PushNotificationService _service;

    public MessageController(PushNotificationService service)
    {
        _service = service;
    }
    
    [AllowAnonymous]
    [HttpPost]
    public async Task<bool> SendNotificationAsync([FromQuery] string token, string title,  string message, string clientId)
    {
        await _service.SendAsync(token, title, message, clientId);
        return true;
    }
}