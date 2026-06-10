using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Services;
using Twilio.AspNet.Common;
using Twilio.AspNet.Core;
using Twilio.Security;

namespace Controllers;

[Route("/twilio/v1/[controller]")]
public class SMSController : TwilioController
{
    private readonly ILogger<SMSController> _logger;
    private readonly SMSService _service;

    public SMSController(
        ILogger<SMSController> logger,
        SMSService service
    )
    {
        _logger = logger;
        _service = service;
    }

    [HttpPost("/twilio/v1/Entity({id})/[controller]")]
    public async Task<IActionResult> IncomingSMS(Guid id, SmsRequest incomingMessage)
    {
        // TODO: check authenticity
        // ...
        
        _logger.LogInformation("Received message for {id}: {message}", id, JsonConvert.SerializeObject(incomingMessage));
        
        var messagingResponse = await _service.OnReceivedAsync(id, incomingMessage);
        return messagingResponse != null ? TwiML(messagingResponse) : NoContent();
    }

    [HttpPost("/twilio/v1/[controller]({id})/Status")]
    public async Task SendStatusCallback([FromRoute] Guid id, SmsStatusCallbackRequest request)
    {
        // TODO: check authenticity
        // ...
        
        _logger.LogInformation("SMS Status for {id}: {Status}", id, request.MessageStatus);

        await _service.OnStatusChangeAsync(id, request);
    }

    [HttpPost("Status")]
    public void StatusCallback(SmsStatusCallbackRequest request)
    {
        _logger.LogInformation("SMS Status: {Status}", request.MessageStatus);
        
        // TODO: do we need a fallback?
        // ...
    }

    private async Task<bool> ValidateTokenAsync(string authToken)
    {
        var httpContext = HttpContext;
        var request = httpContext.Request;
        
        var requestUrl = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        Dictionary<string, string> parameters = null;
        
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);
            parameters = form.ToDictionary(p => p.Key, p => p.Value.ToString());
        }
        
        var signature = request.Headers["X-Twilio-Signature"];
        var validator = new RequestValidator(authToken);
        return validator.Validate(requestUrl, parameters, signature);
    }
}