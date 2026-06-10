using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Authorize("rest")]
// [ApiExplorerSettings(IgnoreApi = true)]
[ApiExplorerSettings(GroupName = "rest")]
[Route("/app/api/Object")]
public class ApiObjectDataFormController : APIController
{
    private readonly ObjectTypeService _objectTypeService;

    public ApiObjectDataFormController(ObjectTypeService objectTypeService)
    {
        _objectTypeService = objectTypeService;
    }
    
    /// <summary>
    /// Get named Object Form (for object) 
    /// </summary>
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{formName}/DataForm")]
    [UseApiNames]
    public async Task<Form> GetObjectFormAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] string formName)
    {
        return await _GetDataFormAsync(objectTypeName, objectId, formName);
    }

    /// <summary>
    /// Get named form (including Add)
    /// </summary>
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{formName}/DataForm")]
    [UseApiNames]
    public async Task<Form> GetObjectTypeFormAsync([FromRoute] string objectTypeName, [FromRoute] string formName)
    {
        return await _GetDataFormAsync(objectTypeName, null, formName);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{name}/DataForm")]
    [UseApiNames]
    // [ProducesResponseType(typeof(DataFormActionResponse), 200)]
    public async Task<DataFormActionResponse> ExecuteObjectFormAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] string name, [FromBody] DataFormActionRequest request)
    {
        return await _ExecuteObjectFormAsync(objectTypeName, objectId, name, request);
    }

    /// <summary>
    /// Execute action on named forms (including Add)
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{name}/DataForm")]
    [UseApiNames]
    // [ProducesResponseType(typeof(DataFormActionResponse), 200)]
    public async Task<DataFormActionResponse> ExecuteObjectTypeFormAsync([FromRoute] string objectTypeName, [FromRoute] string name, [FromBody] DataFormActionRequest request)
    {
        return await _ExecuteObjectFormAsync(objectTypeName, null, name, request);
    }
    
    private async Task<Form> _GetDataFormAsync(string objectTypeName, Guid? id, string formName)
    {
        var options = new GetFormOptions
        {
            SkipLoadingCustomForm = true, // ???
            UseFieldApiNames = true,
            SkipToNextUrlWhenNotForm = false,
        };

        return await _objectTypeService.GetDataFormAsync(Context, objectTypeName, id, formName, Request, options);
    }

    private async Task<DataFormActionResponse> _ExecuteObjectFormAsync(string objectTypeName, Guid? id, string formName, DataFormActionRequest request)
    {
        var options = new GetFormOptions
        {
            SkipLoadingCustomForm = true, // ???
            UseFieldApiNames = true,
            SkipToNextUrlWhenNotForm = false,
        };

        if (id.HasValue)
        {
            request.SelectedIds =
            [
                id.Value
            ];
        }
        else
        {
            request.SelectedIds = null;
        }

        return await _objectTypeService.ExecObjectActionAsync(Context, objectTypeName, request, options);
    }
}