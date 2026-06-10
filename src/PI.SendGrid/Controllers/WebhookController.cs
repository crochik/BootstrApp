using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Models;
using Services;
using StrongGrid;

namespace Controllers;

[Route("/sendgrid/v1/[controller]")]
public class WebhookController : APIController
{
    private readonly SendGridEmailService _emailService;

    public WebhookController(SendGridEmailService emailService)
    {
        _emailService = emailService;
    }
    
    [HttpPost]
    [HttpPost("{accountId}")]
    public async Task<IActionResult> EventAsync([FromRoute] Guid accountId)
    {
        var context = new AccountContext(accountId);
        
        string signature = Request.Headers[WebhookParser.SIGNATURE_HEADER_NAME]; // SIGNATURE_HEADER_NAME is a convenient constant provided so you don't have to remember the name of the header
        string timestamp = Request.Headers[WebhookParser.TIMESTAMP_HEADER_NAME]; // TIMESTAMP_HEADER_NAME is a convenient constant provided so you don't have to remember the name of the header
        string requestBody;
        using (var streamReader = new StreamReader(Request.Body))
        {
            requestBody = await streamReader.ReadToEndAsync().ConfigureAwait(false);
        }

        await _emailService.ParseEventsAsync(context, requestBody, signature, timestamp);

        return new OkResult();
    }
}