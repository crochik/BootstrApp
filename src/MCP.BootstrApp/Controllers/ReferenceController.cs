using McpServer.Resources.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Models;

namespace MCP.Controllers;

[Authorize]
[ApiController]
[Route("~/[controller]")]
public class ReferenceController : ControllerBase
{
    [HttpGet("api-script.g4")]
    [McpResource(Name = "BootstrApp api script grammar", Description = "the script grammar supported by the BootstrApp backend in ANTLR4 format. ", MimeType = "text/x-antlr4", RequiresAuthentication = true)]
    public async Task<string> ApiScriptGrammar(IEntityContext ctx) => await System.IO.File.ReadAllTextAsync("Content/script.g4");
    
    [HttpGet("api-script.md")]
    [McpResource(Name = "BootstrApp api script rule book", Description = "the curated 'what's allowed' / 'not allowed' summary rules doc for the BootstrApp script", MimeType = "text/markdown", RequiresAuthentication = true)]
    public async Task<string> ApiScriptInstructions(IEntityContext ctx) => await System.IO.File.ReadAllTextAsync("Content/script.md");

    [HttpGet("filter-operators.json")]
    [McpResource(Name = "Schema for filter operators", Description = "filter operator list and value rules", MimeType = "application/json", RequiresAuthentication = true)]
    public async Task<string> FilterOperators(IEntityContext ctx) => await System.IO.File.ReadAllTextAsync("Content/filter-operators.json");

    [HttpGet("action-scopes.json")]
    [McpResource(Name = "Action scopes", Description = "Scope rules for actions exposed by BootstrApp", MimeType = "application/json", RequiresAuthentication = true)]
    public async Task<string> ActionScopes(IEntityContext ctx) => await System.IO.File.ReadAllTextAsync("Content/action-scopes.json");

    [HttpGet("schema-conventions.md")]
    [McpResource(Name = "Schema Conventions", Description = "Schema conventions to create object classes in BootstrApp", MimeType = "text/markdown", RequiresAuthentication = true)]
    public async Task<string> SchemaConventions(IEntityContext ctx) => await System.IO.File.ReadAllTextAsync("Content/schema-conventions.md");

    // TODO: should be dynamic
    // ...
    // [HttpGet("platform-modules.d.ts")]
    // [McpResource(Name = "Types and Functions for Action scripts", Description = "Types and Functions available to scripts added as actions in BootstrApp", MimeType = "application/typescript", RequiresAuthentication = true)]
    // public async Task<string> PlatformFunctions(IEntityContext ctx) => await System.IO.File.ReadAllTextAsync("Content/platform-modules.d.ts");
    
    [HttpGet("auth-config.json")]
    [McpResource(Name = "Auth configuration", Description = "Auth configuration to be used by applications when authenticating using BooststrApp", MimeType = "application/json", RequiresAuthentication = true)]
    public async Task<string> AuthConfiguration(IEntityContext ctx) => await System.IO.File.ReadAllTextAsync("Content/auth-config.json");
}
