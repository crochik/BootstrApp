using System.Threading.Tasks;
using Google.Apis.HangoutsChat.v1.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;

namespace PI.Google.Controllers;

[Microsoft.AspNetCore.Components.Route("/google/v1/[controller]")]
public class SpaceController : APIController
{
    private readonly GoogleClient _client;

    public SpaceController(GoogleClient client)
    {
        _client = client;
    }
    
    [Authorize("admin")]
    [HttpPost("Message")]
    public async Task<object> GetAsync([FromBody] SpaceMessage body)
    {
        var message = new Message
        {
            Text = body.Text,
        };
        
        return await _client.SendAsync(body.Space, message, body.Key, body.Token);
    }

    public class SpaceMessage
    {
        public string Space { get; set; }
        public string Key { get; set; }
        public string Token { get; set; }
        public string Text { get; set; }
    }
}
