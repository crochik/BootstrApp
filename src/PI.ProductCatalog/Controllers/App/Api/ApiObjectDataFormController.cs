using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Authorize("rest")]
// [ApiExplorerSettings(IgnoreApi = true)]
[ApiExplorerSettings(GroupName = "rest")]
[Route("/productcatalog/api/Object")]
public class ApiObjectDataFormController : APIController
{
    private readonly ILogger<ApiObjectDataFormController> _logger;
    private readonly ObjectTypeService _objectTypeService;
    private readonly IEnumerable<IFormInterceptor> _interceptors;

    public ApiObjectDataFormController(ILogger<ApiObjectDataFormController> logger, ObjectTypeService objectTypeService, IEnumerable<IFormInterceptor> interceptors)
    {
        _logger = logger;
        _objectTypeService = objectTypeService;
        _interceptors = interceptors;
    }

    /// <summary>
    /// Get named Object Form (for object) 
    /// </summary>
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{formName}/DataForm")]
    [UseApiNames]
    public Task<Form> GetObjectFormAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] string formName)
    {
        return _GetDataFormAsync(objectTypeName, objectId, formName);
    }

    /// <summary>
    /// Get named form (including Add)
    /// </summary>
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{formName}/DataForm")]
    [UseApiNames]
    public Task<Form> GetObjectTypeFormAsync([FromRoute] string objectTypeName, [FromRoute] string formName)
    {
        return _GetDataFormAsync(objectTypeName, null, formName);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{name}/DataForm")]
    [UseApiNames]
    public Task<DataFormActionResponse> ExecuteObjectFormAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] string name, [FromBody] DataFormActionRequest request)
    {
        return _ExecuteObjectFormAsync(objectTypeName, objectId, name, request);
    }

    /// <summary>
    /// Execute action on named forms (including Add)
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{name}/DataForm")]
    [UseApiNames]
    public Task<DataFormActionResponse> ExecuteObjectTypeFormAsync([FromRoute] string objectTypeName, [FromRoute] string name, [FromBody] DataFormActionRequest request)
    {
        return _ExecuteObjectFormAsync(objectTypeName, null, name, request);
    }

    private async Task<Form> _GetDataFormAsync(string objectTypeName, Guid? id, string formName)
    {
        var options = new GetFormOptions
        {
            Cache = new GetFormCache(),
            UseFieldApiNames = true,
            SkipLoadingCustomForm = true,
            SkipToNextUrlWhenNotForm = false,
        };

        var form = await _objectTypeService.GetDataFormAsync(Context, objectTypeName, id, formName, Request, options);

        var interceptors = _interceptors
            .OfType<IInterceptPrepareForm>()
            .Where(x => x.ObjectTypeName == objectTypeName &&
                        (x.FormNames == null || x.FormNames.Any(f => f == formName)))
            .ToArray();

        foreach (var interceptor in interceptors)
        {
            var response = await interceptor.PrepareFormAsync(Context, formName, form, Request);
            if (response == null) continue;

            if (response.IsError) return Form.BuildErrorForm(response.Status);
            form = response.Value;
        }

        return form;
    }

    private async Task<DataFormActionResponse> _ExecuteObjectFormAsync(string objectTypeName, Guid? id, string formName, DataFormActionRequest request)
    {
        var options = new GetFormOptions
        {
            Cache = new GetFormCache(),
            UseFieldApiNames = true,
            SkipLoadingCustomForm = true,
            SkipToNextUrlWhenNotForm = false,
            LoadLayout = false,
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

        var action = request.Action;
        var interceptors = _interceptors
            .Where(x => x.ObjectTypeName == objectTypeName &&
                        (x.FormNames == null || x.FormNames.Any(f => f == formName)) &&
                        (x.ActionNames == null || x.ActionNames.Any(a => a == action)))
            .ToArray();

        foreach (var interceptor in interceptors.OfType<IInterceptBefore>())
        {
            var validated = await interceptor.ValidateRequestAsync(Context, objectTypeName, id, objectTypeName, request);
            if (validated.IsError) return DataFormActionResponse.Error(request, validated.Status);
            request = validated.Value;
        }

        var result = await _objectTypeService.ExecObjectActionAsync(Context, objectTypeName, request, options);
        foreach (var interceptor in interceptors.OfType<IInterceptAfter>())
        {
            var response = await interceptor.ProcessResponseAsync(Context, objectTypeName, id, formName, request, result);
            if (response.IsError) return DataFormActionResponse.Error(request, response.Status);
            result = response.Value;
        }
        
        return result;
    }
}