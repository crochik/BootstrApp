using Controllers;
using Crochik.Mongo;
using MCP.Services;
using McpServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Services.OpenApiGenerator;
using ScriptInterpreter;
using ScriptInterpreter.Generation;
using ScriptInterpreter.Plugins;
using ExecutionContext = ScriptInterpreter.Execution.ExecutionContext;

namespace MCP.Controllers;

[Route("/BootstrApp")]
public class TestController(
    ILogger<TestController> logger,
    MongoConnection connection
) : APIController
{
    [Authorize]
    [HttpGet("{applicationName}/openapi.yml")]
    public async Task<IActionResult> GenerateDocAsync([FromServices] BootstrAppService service, [FromServices] OpenApiSpecGenerator generator, [FromRoute] string applicationName)
    {
        var context = Context;

        generator.SetInfo("BootstrApp.cloud", "BootstrApp.cloud API Client");
        generator.SetServers("https://api.fci.cloud", "https://api.inspirenet.cloud");
        generator.AddSecurity();

        // add system
        generator.EntityContext = context;
        var options = new OpenApiSpecGenerator.AddSchemaOptions
        {
            OverrideRBAC = new AccountContext(context.AccountId.Value),
            AddRequiredFields = true,
            BaseNamespace = "api.system",
            AddDataForm = false,
            Endpoints =
            [
                ActionEndpoint.Filter,
                ActionEndpoint.Create,
                ActionEndpoint.Update,
                ActionEndpoint.Delete,
            ],
        };

        await generator.AddSystemObjectTypesAsync(options, [
            "DataFormActionResponse",
        ]);

        var app = await service.GetAppAsync(context, applicationName);
        if (app == null) throw new McpToolException("Application not found");

        // based on profile
        generator.EntityContext = ProfileContext.Create(app.ProfileId, context.AccountId.Value, context.UserId.Value, context.ClientId);
        options = new OpenApiSpecGenerator.AddSchemaOptions
        {
            AddUserActionOperations = true,
            AddDataForm = false,
            Endpoints =
            [
                ActionEndpoint.Filter,
                ActionEndpoint.Create,
                ActionEndpoint.Update,
                ActionEndpoint.Delete,
            ],
        };

        generator.AddDefaultSchemas();
        await generator.AddSchemasAsync(options);
        await generator.AddDependenciesAsync(options);

        var outputString = await generator.Document.SerializeAsYamlAsync(OpenApiSpecVersion.OpenApi3_0);
        return Content(outputString, "text/x-yaml");
    }

    [Authorize]
    [HttpPost("Account({accountId})")]
    public async Task<IActionResult> ImportAccount([FromServices] BootstrAppService service, [FromRoute] Guid accountId, CancellationToken cancellationToken)
    {
        var result = await service.ProvisionAccountAsync(new AccountContext(accountId), cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{applicationName}/Validate")]
    public async Task<IActionResult> ValidateAppAsync([FromServices] BootstrAppService service, [FromRoute] string applicationName, CancellationToken cancellationToken)
    {
        var app = await service.GetAppAsync(Context, applicationName);
        if (app == null) return NotFound($"{applicationName} not found");
        var result = await service.ValidateObjectTypesAsync(app, cancellationToken);
        if (result.IsError) return BadRequest(result.Status);

        result = await service.ValidateScriptsAsync(app, cancellationToken);
        if (result.IsError) return BadRequest(result.Status);

        return Ok(result);
    }

    // [Authorize]
    [HttpGet("~/Script")]
    public async Task<IActionResult> GenerateTypescript(CancellationToken cancellationToken, [FromServices] ObjectTypeScriptPlugin plugin)
    {
        // var accountContext = new AccountContext(AccountIds.FCI);
        var accountContext = new AccountContext(Guid.Parse("6355a537-a47c-42b1-af6b-7d2febc00895"));
        await plugin.LoadObjectTypesAsync(accountContext, "/^app\\./");

        var env = ScriptEnvironment.FromPlugins(plugin, new SystemDatePlugin());
        var dts = new TypeScriptDeclarationGenerator(env).Generate();

        return Content(dts, "text/plain"); // "application/x-typescript"
    }

    // [Authorize]
    [HttpPost("~/Script/Validate")]
    public async Task<IActionResult> ValidateTypescript(CancellationToken cancellationToken, [FromServices] ObjectTypeScriptPlugin plugin)
    {
        var accountContext = new AccountContext(Guid.Parse("6355a537-a47c-42b1-af6b-7d2febc00895"));
        await plugin.LoadObjectTypesAsync(accountContext, "/^app\\./"); // Context
        var env = ScriptEnvironment.FromPlugins(plugin);
        var result = await env.ValidateAsync(Request.GetBody(), cancellationToken);

        return Ok(result);
    }

    [HttpPost("~/Script/Run")]
    public async Task<IActionResult> RunTypescript(CancellationToken cancellationToken, [FromServices] ObjectTypeScriptPlugin plugin)
    {
        plugin.EntityContext = new AccountContext(Guid.Parse("6355a537-a47c-42b1-af6b-7d2febc00895"));
        plugin.Namespaces.Add("app.stardeck");
        // await service.LoadObjectTypesAsync(accountContext, "/^app\\./"); // Context

        var context = new ExecutionContext
        {
        };

        context.SetValueDeep("input", new Dictionary<string, object?>
        {
            // {"id", "test"},
            { "name", "Project #1" },
            { "description", "Test Project #1" },
            // {"newStatusId", "status"},
            // {"newDisplayOrder", null}
        });

        var env = ScriptEnvironment.FromPlugins(plugin);

        var result = await env.ExecuteAsync(Request.GetBody(), context, cancellationToken);
        return Ok(result);
    }
}