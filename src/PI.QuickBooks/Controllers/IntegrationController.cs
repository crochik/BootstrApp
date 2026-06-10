using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.QuickBooks.Services;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.SingleUseTickets;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Produces("application/json")]
[Route("/quickbooks/v1/[controller]")]
public class IntegrationController : APIController
{
    private readonly ObjectTypeService _objectTypeService;
    private readonly QuickBooksService _service;
    
    public IntegrationController(
        ObjectTypeService objectTypeService,
        QuickBooksService service
    )
    {
        _objectTypeService = objectTypeService;
        _service = service;
    }
    
    [Authorize("manager")]
    [HttpGet("DataForm")]
    public async Task<Form> GetLoginDataFormAsync()
    {
        var integration = await _service.GetIntegrationAsync(Context);
        if (integration != null)
        {
            var objectType = await _objectTypeService.GetAsync(Context, QuickBooksIntegrationConfiguration.IntegrationObjectTypeName);
            var form =  await _objectTypeService.GetEditDataFormAsync(Context, objectType, integration.Id, objectType.CanUpdate(Context) ? FormName.Edit : FormName.Details);
            if (form != null)
            {
                form.Actions = (form.Actions ?? Enumerable.Empty<FormAction>())
                    .Append(new FormAction
                    {
                        Name = "Login",
                        Label = "Reconnect",
                        Action = "Login"
                    })
                    .ToArray();
            }

            return form;
        }

        return new Form
        {
            Name = "QuickBooks",
            Title = "QuickBooks",
            Fields = new FormField[]
            {
                new LabelField
                {
                    Name = "Message",
                    Label = "Connect your QuickBooks account so we can exchange information between the two systems",
                },
                new LabelField
                {
                    Name = "Instructions",
                    Label = "After clicking Start we will open a new tab for you to continue the process",
                },
                // new CheckboxField
                // {
                //     Name = "Sandbox",
                //     Label = "Use Sandbox",
                // }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Login",
                    Label = "Start",
                },
            }
        };
    }

    [HttpPost("DataForm")]
    [Authorize("manager")]
    public async Task<DataFormActionResponse> GenerateStartUrlAsync([FromBody] DataFormActionRequest request)
    {
        if (request.Action == "Login")
        {
            // var sandbox = false;
            // if (request.TryGetParam("Sandbox", out var value))
            // {
            //     sandbox = value switch
            //     {
            //         bool b => b,
            //         string s => bool.TryParse(s, out var b) && b,
            //         _ => false,
            //     };
            // }

            var login = await _service.StartLoginAsync(Context);
            if (!login.IsSuccess)
            {
                return DataFormActionResponse.Error(request, login.Status);
            }

            return new DataFormActionResponse(request, "Launching on new Browser tab")
            {
                NextUrl = login.Value,
                Success = true,
            };
        }
        
        if (request.Action == FormAction.Update)
        {
            return await _objectTypeService.ExecObjectActionAsync(Context, QuickBooksIntegrationConfiguration.IntegrationObjectTypeName, request);
        }

        return DataFormActionResponse.Error(request, "Invalid Action");
    }

    [HttpGet("redirect")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback()
    {
        if (!Request.Query.TryGetValue("state", out var stateStr))
        {
            return BadRequest("Missing State");
        }

        // if (!Request.Query.TryGetValue("code", out var code)) return BadRequest("Missing code");

        var result = await _service.LoginAsync(stateStr.FirstOrDefault(), Request.QueryString.ToString());
        if (!result.IsSuccess)
        {
            return Ok($"Authentication Error: {result.Status}");
        }
        
        return Ok("QuickBooks integration added to Account. You can close this tab");
    }
}

public class QuickbooksSingleUseTicket : SingleUseTicket
{
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>
    /// The state.
    /// </value>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the start URL.
    /// </summary>
    /// <value>
    /// The start URL.
    /// </value>
    public string StartUrl { get; set; }

    /// <summary>
    /// Gets or sets the code verifier.
    /// </summary>
    /// <value>
    /// The code verifier.
    /// </value>
    public string CodeVerifier { get; set; }

    /// <summary>
    /// Gets or sets the redirect URI.
    /// </summary>
    /// <value>
    /// The redirect URI.
    /// </value>
    public string RedirectUri { get; set; }

    /// <summary>
    /// Whether is a sandbox account or not (production)
    /// </summary>
    public bool UseSandbox { get; set; }
}

public class QuickBooksIntegrationConfiguration : IntegrationConfigurationWithToken
{
    public const string IntegrationObjectTypeName = $"quickbooks.IntegrationConfiguration";
    public const string ProtectionPurpose = $"EntityIntegration.{nameof(IntegrationIds.QuickBooks)}"; // "EntityIntegration.Configuration";
    public const string SandboxBaseUrl =  "https://sandbox-quickbooks.api.intuit.com/";
    public const string ProductionBaseUrl =  "https://??????quickbooks.api.intuit.com/"; // TODO: ???
    
    public string CompanyId { get; set; }

    /// <summary>
    /// Whether is a sandbox account or not (production)
    /// </summary>
    public bool UseSandbox { get; set; }

    public string MinorVersion => "73";

    public string BaseUrl => UseSandbox ? SandboxBaseUrl : ProductionBaseUrl;
}