using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;
using Services;

namespace Controllers.App.Api;

[Authorize("rest")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("/salesforce/api/Object")]
public class AppApiObjectController : APIController
{
    /// <summary>
    /// Get named Object Form (for object) 
    /// </summary>
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{name}/DataForm")]
    [UseApiNames]
    public async Task<Form> GetObjectFormAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] string name, [FromServices] HybridSalesforceObjectEditor editor)
    {
        var result = Result.Error<Form>($"Form {name} not supported.");
        if (Enum.TryParse(name, out FormName formName))
        {
            result = formName switch
            {
                FormName.Add => Result.Error<Form>($"Form {formName} does not expect object id."),
                _ => await editor.BuildFormAsync(Context, objectTypeName, objectId, formName),
            };
        }
        else
        {
            // no support for other named forms for now
            // ...
        }

        return result.IsSuccess ? result.Value : Form.BuildErrorForm(result.Status);
    }

    /// <summary>
    /// Get named form (including Add)
    /// </summary>
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{name}/DataForm")]
    [UseApiNames]
    public async Task<Form> GetObjectTypeFormAsync([FromRoute] string objectTypeName, [FromRoute] string name, [FromServices] HybridSalesforceObjectEditor editor)
    {
        var result = Result.Error<Form>($"Form {name} not supported.");
        if (Enum.TryParse(name, out FormName formName))
        {
            result = formName switch
            {
                FormName.Add => await editor.BuildFormAsync(Context, objectTypeName, null, formName),
                _ => Result.Error<Form>($"Form {name} requires object id."),
            };
        }
        else
        {
            // no support for other named forms for now
            // ...
        }

        return result.IsSuccess ? result.Value : Form.BuildErrorForm(result.Status);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{name}/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> ExecuteObjectFormAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] string name, [FromBody] DataFormActionRequest request, [FromServices] HybridSalesforceObjectEditor editor)
    {
        request.SelectedIds =
        [
            objectId
        ];

        if (Enum.TryParse(name, out FormName _))
        {
            return await editor.ExecUpdateActionAsync(Context, objectTypeName, objectId, request, new ObjectTypeService.UpdateObjectOptions
            {
                UseFieldApiNames = true,
            });
        }

        return DataFormActionResponse.Error(request, $"Form {name} does not support action.");
    }

    /// <summary>
    /// Execute action on named forms (including Add)
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{name}/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> ExecuteObjectTypeFormAsync([FromRoute] string objectTypeName, [FromRoute] string name, [FromBody] DataFormActionRequest request, [FromServices] HybridSalesforceObjectEditor editor)
    {
        await Task.CompletedTask;

        // request.SelectedIds = null;
        //
        // if (Enum.TryParse(name, out FormName _))
        // {
        //     return await _objectTypeService.ExecObjectActionAsync(Context, objectTypeName, request, new GetFormOptions
        //     {
        //         UseFieldApiNames = true,
        //     });
        // }

        return DataFormActionResponse.Error(request, $"{request.Action} not supported for {name} form");
    }
}