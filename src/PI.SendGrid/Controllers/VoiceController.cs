using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Models;
using Services;
using Twilio.AspNet.Common;
using Twilio.AspNet.Core;
using Twilio.TwiML;
using Twilio.TwiML.Voice;

namespace Controllers;

[Route("/twilio/v1/[controller]")]
public class VoiceController : TwilioController
{
    private readonly ILogger<SMSController> _logger;
    private readonly SMSService _smsService;

    public VoiceController(ILogger<SMSController> logger, SMSService smsService)
    {
        _logger = logger;
        _smsService = smsService;
    }
    
    [HttpPost]
    public TwiMLResult IncomingCall(VoiceRequest request)
    {
        var response = new VoiceResponse();
        
        var gather = new Gather(
            numDigits: 1,
            action: new Uri("/twilio/v1/voice/step/zipcode", UriKind.Relative)
        );

        gather.Say($"Welcome to Floor Coverings International. Is your zip code ,{SpelledPhoneNumber(request.FromZip)}?")
            .Say("Press 0 for yes");

        response.Append(gather);

        gather = new Gather(
            numDigits: 5,
            action: new Uri("/twilio/v1/voice/step/zipcode", UriKind.Relative)
        );

        gather.Say($"Please enter your zip code");

        response.Append(gather);

        _logger.LogInformation("Incoming Call: {Request}", JsonConvert.SerializeObject(request, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
        
        return TwiML(response);
    }

    [HttpPost("step/{stepId}")]
    public async System.Threading.Tasks.Task<TwiMLResult> NextStep(VoiceRequest request, [FromRoute] string stepId)
    {   
        _logger.LogInformation("{Step}: {Request}", stepId, JsonConvert.SerializeObject(request, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
        
        var response = new VoiceResponse();

        if (request.Digits == "0" || request.Digits.Length==5)
        {
            var msg = request.Digits.Length == 5 ?
                $"Forwarding call from {request.From} ({request.CallerName}).\nZip: {request.Digits}\n" :
                $"Forwarding call from {request.From} ({request.CallerName}).\nZip: {request.FromZip}\nCity: {request.FromCity}\nState: {request.FromState}";
            
            await _smsService.SendAsync(new AccountContext(AccountIds.FCI), request.From, msg, new KeyValuePair<string, object>[] { });

            response.Say("Great. Transferring you.");
            response.Dial("919-412-5903", timeout: 5, callerId: request.From );
            response.Say("Sorry, The Human is sleeping");
            response.Hangup();
        }
        else
        {
            var gather = new Gather(
                numDigits: 5,
                action: new Uri("/twilio/v1/voice/step/zipcode", UriKind.Relative)
            );
            
            gather.Say($"Please enter your zip code");
            response.Append(gather);
        }

        return TwiML(response);
    }

    [HttpPost("Status")]
    public void StatusCallback(StatusCallbackRequest request)
    {
        _logger.LogInformation("Voice Status: {From}", request.From);
        
        _logger.LogInformation("Status: {Request}", JsonConvert.SerializeObject(request, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));

    }
    
    private static string SpelledPhoneNumber(string phoneNumber)
    {
        return string.Join(", ", phoneNumber.ToCharArray());
    }
}