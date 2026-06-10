using System.Text;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IDP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IDP.Controllers;

[Route("[controller]/[action]")]
public class HomeController(
    ILogger<HomeController> logger,
    IIdentityServerInteractionService interactionService,
    IWebHostEnvironment environment   
    ) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Error(string message, string errorId)
    {
        ErrorMessage errorMessage = null;
        if (!string.IsNullOrEmpty(errorId))
        {
            errorMessage = await interactionService.GetErrorContextAsync(errorId);
            logger.LogError("Login Error {ErrorId}: {Message}", errorMessage?.Error, errorMessage?.ErrorDescription);
        }

        if (!string.IsNullOrEmpty(message))
        {
            logger.LogError("Login Error {Message}", message);
        }

        return Redirect($"/loginerror.html?error={errorMessage?.Error}");
    }
    
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Echo()
    {
        var dump = Request.Dump();
        logger.LogInformation("Echo: {Request}", dump);
        return Ok(dump);
    }
    
    [HttpGet("Error")]
    public async Task<IActionResult> Error(string errorId)
    {
        var vm = new ErrorViewModel();

        // Retrieve error details from IdentityServer
        var message = await interactionService.GetErrorContextAsync(errorId);
        if (message != null)
        {
            vm.Error = message;

            // Hide sensitive details outside development
            if (!environment.IsDevelopment())
            {
                message.ErrorDescription = null;
            }
        }

        return View("Error", vm);
    }    
}

public static class HttpRequestExtensions
{
    public static string Dump(this HttpRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("--- REQUEST DUMP ---");
        sb.AppendLine($"Protocol: {request.Protocol}");
        sb.AppendLine($"Scheme: {request.Scheme}");
        sb.AppendLine($"Host: {request.Host}");
        sb.AppendLine($"Method: {request.Method}");
        sb.AppendLine($"Path: {request.Path}");
        sb.AppendLine($"QueryString: {request.QueryString}");

        sb.AppendLine("--- HEADERS ---");
        foreach (var header in request.Headers)
        {
            sb.AppendLine($"{header.Key}: {header.Value}");
        }

        // You can also add more details like the scheme, host, and body
        // sb.AppendLine($"Scheme: {request.Scheme}");
        // sb.AppendLine($"Host: {request.Host}");

        // If you need to read the body, be mindful that it's a stream and can only be read once.
        // You might need to enable buffering to read it multiple times.
        // request.EnableBuffering();
        // var body = new StreamReader(request.Body).ReadToEndAsync().Result;
        // sb.AppendLine($"Body: {body}");

        return sb.ToString();
    }
}