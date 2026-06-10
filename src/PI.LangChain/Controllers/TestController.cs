using System;
using System.Dynamic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.LangChain.Models;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.LangChain.Controllers;

[Route("/langchain/v1/[controller]")]
public class TestController : APIController
{
    private readonly ObjectTypeService _objectTypeService;
    private readonly MongoConnection _connection;

    public TestController(ObjectTypeService objectTypeService, MongoConnection connection)
    {
        _objectTypeService = objectTypeService;
        _connection = connection;
    }

    [Authorize("admin")]
    [HttpPost("{objectTypeName}({objectId})/Document/{templateName}")]
    public async Task<ActionResult> Generate([FromRoute] string objectTypeName, Guid objectId, [FromRoute] string templateName, [FromServices] DocumentTemplateService service)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        var obj = await _objectTypeService.GetExpandoObjectByIdAsync(Context, objectType, objectId);

        var docTemplate = await _connection.GetProfileElementAsync<DocumentTemplateProfileElement>(Context, templateName);
        var result = service.Generate(Context, docTemplate, obj);
        
        return Content(result, "text/plain");
    }
    
    [Authorize("admin")]
    [HttpPost("Document/{templateName}")]
    public async Task<ActionResult> Generate([FromRoute] string templateName, [FromBody] ExpandoObject objectContext, [FromServices] DocumentTemplateService service)
    {
        var docTemplate = await _connection.GetProfileElementAsync<DocumentTemplateProfileElement>(Context, templateName);
        
        var result = service.Generate(Context, docTemplate, objectContext);
        return Content(result, "text/plain");
    }
}