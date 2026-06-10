using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using Services;

namespace Controllers;

/// <summary>
/// Methods overriden from API using apiPath
/// </summary>
[Route("/salesforce/v1/[controller]")]
public class CustomObjectController : APIController
{
    /// <summary>
    /// Edit Object (id in query or as part of the route) 
    /// </summary>
    [Authorize("default")]
    [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/{formName}/DataForm")]
    [HttpGet("/salesforce/v1/[controller]({objectTypeName:regex(^[[a-z_0-9\\.]]+$)})/DataForm")]
    [HttpGet("/salesforce/v1/[controller]({objectTypeName:regex(^[[a-z_0-9\\.]]+$)})/{formName}/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}({objectId})/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}({objectId})/{formName}/DataForm")]
    public async Task<Form> GetEditFormAsync([FromRoute] string objectTypeName, [FromQuery] string id, [FromRoute] string objectId, [FromServices] HybridSalesforceObjectEditor editor, [FromRoute] FormName formName = FormName.Edit)
    {
        objectId ??= id;
        if (string.IsNullOrWhiteSpace(objectId))
        {
            formName = FormName.Add;
        }

        var result = await editor.BuildFormAsync(Context, objectTypeName, objectId, formName);
        
        return result.IsSuccess ? result.Value : Form.BuildErrorForm(result.Status);
    }

    /// <summary>
    /// Execute action (id was in query or route)
    /// </summary>
    [Authorize("default")]
    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/DataForm")]
    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/{formName}/DataForm")]
    [HttpPost("/salesforce/v1/[controller]({objectTypeName:regex(^[[a-z_0-9\\.]]+$)})/DataForm")]
    [HttpPost("/salesforce/v1/[controller]({objectTypeName:regex(^[[a-z_0-9\\.]]+$)})/{formName}/DataForm")]
    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}({objectId})/DataForm")]
    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}({objectId})/{formName}/DataForm")]
    public async Task<DataFormActionResponse> EditFormOnActionAsync([FromRoute] string objectTypeName, [FromRoute] string objectId, [FromBody] DataFormActionRequest request, [FromServices] HybridSalesforceObjectEditor editor)
    {
        if (string.IsNullOrEmpty(objectId))
        {
            if (request.SelectedIds?.Length != 1)
            {
                if (request.TryGetParam(Model.IdFieldName, out var idObj) && idObj != null)
                {
                    objectId = idObj.ToString();   
                }
                else
                {
                    return DataFormActionResponse.Error(request, "Invalid Id");
                }
            }
            else
            {
                objectId = request.SelectedIds[0].ToString();
            }
        }

        if (request.Action != FormAction.Update)
        {
            return DataFormActionResponse.Error(request, $"{request.Action} not supported");
        }
        
        if (!Guid.TryParse(objectId, out var id))
        {
            // TODO: could add code to handle external id?
            // ...
            
            return DataFormActionResponse.Error(request, "Invalid id");
        }

        return await editor.ExecUpdateActionAsync(Context, objectTypeName, id, request);
    }
}