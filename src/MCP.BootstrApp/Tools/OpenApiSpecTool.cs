using System.Text.Json.Serialization;
using Crochik.Mongo;
using MCP.Services;
using McpServer.Models;
using McpServer.Tools.Attributes;
using Microsoft.OpenApi;
using PI.Shared.Models;
using PI.Shared.Services.OpenApiGenerator;
using ScriptInterpreter;
using ScriptInterpreter.Generation;

namespace McpServer.Tools;

public class OpenApiSpecTool(
    ILogger<OpenApiSpecTool> logger,
    MongoConnection connection,
    IServiceProvider serviceProvider,
    BootstrAppService appService,
    SingleUseFileAccessService singleUseFileAccessService,
    ObjectTypeScriptPlugin scriptPlugin
) 
{
    private string BasePath => "https://rproxy-fci.fci.cloud";

    [McpTool(
        Name = "generate_openapi_spec",
        Description = "Generate the openapi spec for all the object classes and actions so it can be used locally to generate the api client. The response provides a URL so the file can be fetched and saved locally.",
        ExamplePrompts =
        [
            "Update the OpenApi spec for my app",
            "Generate the openapi spec so I can generate the API client for my app"
        ],
        StructuredOutput = true
        )
    ]
    public async Task<GenerateFileResponse> GetOpenAPISpecAsync(
        IEntityContext context,
        [McpParameter(Description = "Application name", Required = true)]
        string applicationName
    )
    {
        var app = await appService.GetAppAsync(context, applicationName);
        if (app == null) throw new McpToolException($"{applicationName} not found or hasn't been initialized");
        
        var accountContext = new AccountContext(app.AccountId).WithActorFrom(context);
        
        var result =  await appService.ValidateObjectTypesAsync(app);
        if (result.IsError)
        {
            throw new McpToolException($"{applicationName} validation failed. {result.Status}");
        }
                
        var generator = serviceProvider.GetRequiredService<OpenApiSpecGenerator>();
        generator.SetInfo("BootstrApp.cloud", "BootstrApp.cloud API Client");
        generator.SetServers("https://api.fci.cloud", "https://api.inspirenet.cloud");
        generator.AddSecurity();

        // add system
        generator.EntityContext = accountContext;
        var options = new OpenApiSpecGenerator.AddSchemaOptions
        {
            OverrideRBAC = new AccountContext(accountContext.AccountId.Value),
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
            // "Entity", //  TODO: Create shadow object on the same namespace ...
            // "User", //  TODO: Create shadow object on the same namespace ...
            // "Organization", //  TODO: Create shadow object on the same namespace ...
        ]);

        // based on profile
        generator.EntityContext = ProfileContext.Create(app.ProfileId, accountContext.AccountId.Value, Guid.Empty, app.ClientId);
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
        
        // limit to app.*
        // await generator.AddSchemasAsync(options);
        var objectTypes = await connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, generator.EntityContext.AccountId)
            .Regex(x => x.Namespace, $"/^app\\./")
            .BitsAnySet(x => x.RBAC.Permissions[generator.EntityContext.ProfileId.ToString()], 0b111 /*CRU*/)
            .IncludeFields(x => x.Name, x => x.Namespace, x => x.Id, x => x.RBAC)
            .FindAsync();
        foreach (var ot in objectTypes)
        {
            await generator.ProcessObjectTypeAsync(ot.FullName, options);
        }
        
        await generator.AddDependenciesAsync(options);
        
        var outputString = await generator.Document.SerializeAsYamlAsync(OpenApiSpecVersion.OpenApi3_0);
        Result<Guid> cached = await singleUseFileAccessService.AddAsync(accountContext, outputString, "application/x-yaml");

        return new GenerateFileResponse
        {
            DownloadURL = $"{BasePath}/BootstrApp/mcp/Download/{cached.Value}",
            SuggestedPath = $"./api/{applicationName}.yaml",
            Instruction = "Download with: curl -fsSL -o <SuggestedPAth> <DownloadURL>. Do not read or cat the file contents — it is large file and you have all the metadata you need."
        };
    }

    [McpTool(
            Name = "generate_platform_definitions",
            Description = "Generate the script platform definitions to use as a reference when generating action scripts. The response provides a URL so the file can be fetched and saved locally.",
            ExamplePrompts =
            [
                "Get all the methods and types defined in the platform that can be used in action scripts",
                "Generate .d.ts file for  application scripts"
            ],
            StructuredOutput = true
        )
    ]
    public async Task<GenerateFileResponse> GetPlatformAsync(
        IEntityContext context,
        [McpParameter(Description = "Application name", Required = true)]
        string applicationName
    )
    {
        var app = await appService.GetAppAsync(context, applicationName);
        if (app == null) throw new McpToolException($"{applicationName} not found or hasn't been initialized");

        var profileContext = ProfileContext.Create(app.ProfileId, app.AccountId, context.UserId.Value, app.ClientId, context.OrganizationId, context.Claims);

        var script = 
@"/**
 * BootstrApp.cloud
 * BootstrApp.cloud API Client
 * @version 0.0.1
 */
"; 
            
        script += await System.IO.File.ReadAllTextAsync("Content/platform-modules.d.ts");
        await scriptPlugin.LoadObjectTypesAsync(profileContext, $"/^app\\.{applicationName}/"); // Context
        
        var env = ScriptEnvironment.FromPlugins(scriptPlugin);
        var dts = new TypeScriptDeclarationGenerator(env).Generate();
        
        script += "\n";
        script += dts;
        
        var cached = await singleUseFileAccessService.AddAsync(profileContext, script, "application/x-typescript");
        
        return new GenerateFileResponse
        {
            DownloadURL = $"{BasePath}/BootstrApp/mcp/Download/{cached.Value}",
            SuggestedPath = $"./script/platform.d.ts",
            Instruction = "Download with: curl -fsSL -o <SuggestedPAth> <DownloadURL>. Do not read or cat the file contents — it is large file and you have all the metadata you need."
        };
    }
    
    
    public class GenerateFileResponse
    {
        [JsonPropertyName("downloadURL")]
        public string DownloadURL { get; set; }
        
        [JsonPropertyName("suggestedPath")]
        public string SuggestedPath { get; set; }
        
        [JsonPropertyName("instruction")]
        public string Instruction { get; set; }
    }
}