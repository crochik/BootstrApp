using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace PI.Shared.Controllers;

public abstract class AbstractObjectTypeController<T> : APIController
    where T : EntityOwnedModel, new()
{
    protected readonly ObjectTypeService _objectTypeService;

    private string ObjectTypeName => typeof(T).Name;


    protected AbstractObjectTypeController(ObjectTypeService objectTypeService)
    {
        _objectTypeService = objectTypeService;
    }

    [Authorize("managerplus")]
    [HttpPost("DataView")]
    [Produces("text/csv", "application/json")]
    public virtual async Task<IDataViewResponse> DataViewAsync([FromBody] DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        Prepare(request);

        var objectType = await _objectTypeService.GetAsync(Context, ObjectTypeName);
        if (objectType == null) throw new NotFoundException($"{ObjectTypeName} not found");
        var response = await builder.BuildDataViewAsync(Context, objectType, request);

        return response;
    }

    [Authorize("managerplus")]
    [HttpPost("Import")]
    [Consumes("application/octet-stream", "multipart/form-data")]
    public virtual async Task<DataFormActionResponse> DataViewImportByIdAsync(IFormFile file)
    {
        var objectType = await _objectTypeService.GetAsync(Context, ObjectTypeName);
        if (objectType == null) throw new NotFoundException($"{ObjectTypeName} not found");
        return await _objectTypeService.ImportCsvAsync(Context, objectType, file.OpenReadStream());
    }

    // [Authorize("default")]
    // [HttpGet("DataForm")]
    // public virtual async Task<PI.Shared.Form.Models.Form> GetEditFormAsync([FromQuery] Guid? id)
    // {
    //     var form = await _objectTypeService.GetDataFormAsync(Context, ObjectTypeName, id);
    //     if (form == null) throw new NotFoundException(ObjectTypeName, id);
    //     return form;
    // }

    // [Authorize("default")]
    // [HttpPost("DataForm")]
    // public virtual async Task<DataFormActionResponse> EditFormOnActionAsync([FromBody] DataFormActionRequest request)
    // {
    //     try
    //     {
    //         var result = await _objectTypeService.ExecObjectActionAsync<T>(Context, request);
    //         return result;
    //     }
    //     catch (Exception ex)
    //     {
    //         return new DataFormActionResponse(request, ex.Message);
    //     }
    // }
}