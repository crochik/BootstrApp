using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.ProductCatalog.Models;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

public abstract class AbstractCatalogFeedController<T> : APIController
    where T : CatalogFeed, new()
{
    protected readonly ObjectTypeService _objectTypeService;

    protected AbstractCatalogFeedController(
        ObjectTypeService objectTypeService
    )
    {
        _objectTypeService = objectTypeService;
    }

    [Authorize("managerplus")]
    [HttpPost("DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<IDataViewResponse> DataViewAsync([FromBody] DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        Prepare(request);

        var objectTypeName = typeof(T).Name;
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException($"{objectTypeName} not found");
                        
        var response = await builder.BuildDataViewAsync(Context, objectType, request);

        return response;
    }

    [Authorize("managerplus")]
    [HttpGet("DataForm")]
    public async Task<Form> GetEditFormAsync([FromQuery] Guid? id)
    {
        var objectType = typeof(T).Name;
        var form = await _objectTypeService.GetDataFormAsync(Context, objectType, id);
        if (form == null) throw new NotFoundException(objectType, id);
        return form;
    }

    protected async Task<DataFormActionResponse> OnActionAsync(DataFormActionRequest request)
    {
        try
        {
            var result = await _objectTypeService.ExecObjectActionAsync<T>(Context, request);
            return result;
        }
        catch (Exception ex)
        {
            return new DataFormActionResponse(request, ex.Message);
        }
    }

    // [Authorize("managerplus")]
    // [HttpPost("{objectType}/Import")]
    // [Consumes("application/octet-stream", "multipart/form-data")]
    // public virtual async Task<DataFormActionResponse> DataViewImportByIdAsync([FromRoute] string objectType, [FromForm] IFormFile file)
    // {
    //     return await _objectTypeService.ImportCsvAsync(Context, objectType, file.OpenReadStream());
    // }
}