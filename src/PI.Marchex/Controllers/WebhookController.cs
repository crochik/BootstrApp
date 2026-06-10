using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Models;

namespace PI.Marchex.Controllers;

[Route("/marchex/v1/[controller]")]
public class WebhookController : APIController
{
    private readonly ILogger<WebhookController> _logger;
    private readonly MongoConnection _connection;

    public WebhookController(ILogger<WebhookController> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    [HttpPost("Call")]
    public async Task<IActionResult> CallAsync([FromBody] ExpandoObject body)
    {
        await AddAsync(body, "Call");
        return Ok();
    }

    [HttpPost("SMS")]
    public async Task<IActionResult> SMSAsync([FromBody] ExpandoObject body)
    {
        await AddAsync(body, "SMS");
        return Ok();
    }
    
    [HttpPost("Custom")]
    public async Task<IActionResult> CustomAsync([FromBody] ExpandoObject body)
    {
        await AddAsync(body, "Custom");
        return Ok();
    }
    
    private async Task AddAsync(ExpandoObject body, string type)
    {
        IActionResult actionResult;
        var evt = new Event
        {   
            Id = Guid.NewGuid(),
            AccountId = AccountIds.FCI,
            EntityId = AccountIds.FCI,
            Properties = body,
            Type = type,
            CreatedOn = DateTime.UtcNow,
        };
        
        foreach (var header in Request.Headers)
        {
            evt.Headers.Add(header.Key, header.Value.Count > 1 ? string.Join(", ", header.Value) : header.Value.FirstOrDefault());
        }

        await _connection.InsertAsync(evt);
    }
}

[BsonCollection("marchex.Event")]
public class Event : EntityOwnedModel
{
    public string Type { get; set; }
    public IDictionary<string, object> Properties { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
}