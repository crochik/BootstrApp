using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Services.OpenApiGenerator;

namespace PI.OpenAPI.Controllers;

[Route("/openapi/v1/[controller]")]
public class GenerateController : APIController
{
    [Authorize("admin")]
    [HttpGet("Generic")]
    public async Task<IActionResult> GenerateGenericAsync([FromServices] OpenApiSpecGenerator generator)
    {
        generator.SetInfo("ProgramInterface.com", "ProgramInterface.com API client");
        generator.SetServers("https://api.fci.cloud", "https://api.inspirenet.cloud");
        generator.AddSecurity();

        generator.EntityContext = Context;
        var options = new OpenApiSpecGenerator.AddSchemaOptions
        {
            OverrideRBAC = new AccountContext(Context.AccountId.Value),
            AddRequiredFields = true,
            BaseNamespace = "api.system",
            SkipOperations = true,
        };

        // ignore field api names
        generator.PropertySchemaNameGenerator = (field) => field.Name;
        
        await generator.AddSystemAsync(options);
        
        var outputString = await generator.Document.SerializeAsYamlAsync(OpenApiSpecVersion.OpenApi3_0);

        return Content(outputString, "application/yaml");
    }
    
    [Authorize("admin")]
    [HttpGet("Profile/{profileId}")]
    public async Task<IActionResult> GenerateClientAsync([FromRoute] Guid profileId, [FromServices] OpenApiSpecGenerator generator, [FromServices] MongoConnection connection)
    {
        generator.SetInfo("ProgramInterface.com", "ProgramInterface.com API client");
        generator.SetServers("https://api.fci.cloud", "https://api.inspirenet.cloud");
        generator.AddSecurity();
        
        // add system
        generator.EntityContext = Context;
        var options = new OpenApiSpecGenerator.AddSchemaOptions
        {
            OverrideRBAC = new AccountContext(Context.AccountId.Value),
            AddRequiredFields = true,
            BaseNamespace = "api.system",
        };        
        await generator.AddSystemAsync(options);
        
        // based on profile
        generator.EntityContext = ProfileContext.Create(profileId, Context.AccountId.Value, Context.UserId.Value, Context.ClientId);
        options = new OpenApiSpecGenerator.AddSchemaOptions
        {
            AddUserActionOperations = true,
        };

        generator.AddDefaultSchemas();
        await generator.AddSchemasAsync(options);
        await generator.AddDependenciesAsync(options);
        
        // var added = generator.Document.Components.Schemas.Keys;
        //
        // await connection.Filter<ObjectType>()
        //     .Eq(x => x.AccountId, Context.AccountId)
        //     .In(x => x.FullName, added)
        //     .Update
        //     .AddToSet(x => x.Tags, "OTG")
        //     .UpdateManyAsync();
        
        var outputString = await generator.Document.SerializeAsYamlAsync(OpenApiSpecVersion.OpenApi3_0);

        return Content(outputString, "application/yaml");
    }
    
    [Authorize("admin")]
    [HttpGet("Profile/{profileId}/Object/{objectTypeName}")]
    public async Task<IActionResult> GenerateForObjectAsync([FromServices] OpenApiSpecGenerator generator, [FromRoute] Guid profileId, [FromRoute] string objectTypeName)
    {
        generator.SetInfo("ProgramInterface.com", "ProgramInterface.com API client");
        generator.SetServers("https://api.fci.cloud", "https://api.inspirenet.cloud");
        generator.AddSecurity();

        generator.EntityContext = ProfileContext.Create(profileId, Context.AccountId.Value, Context.UserId.Value, Context.ClientId);
        var options = new OpenApiSpecGenerator.AddSchemaOptions
        {
            AddUserActionOperations = true,
        };

        await generator.AddSchemaAsync(options, objectTypeName);
        
        var outputString = await generator.Document.SerializeAsYamlAsync(OpenApiSpecVersion.OpenApi3_0);

        return Content(outputString, "application/yaml");
    }

}