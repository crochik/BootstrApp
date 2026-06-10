using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Route("/api/v1/[controller]")]
public class AppDataViewController : APIController
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public AppDataViewController(
        MongoConnection connection,
        ObjectTypeService objectTypeService
    )
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    /// <summary>
    /// Get AppDataView by Id
    /// </summary>
    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]({id:guid})")]
    public async Task<AppDataView> GetByIdAsync([FromRoute] Guid id)
    {
        var dataView = await _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (dataView == null) throw new NotFoundException(nameof(AppDataView), id);

        return dataView;
    }

    /// <summary>
    /// Get AppDataView by Name
    /// </summary>
    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]/{name}")]
    [HttpGet("/api/v1/[controller]({name:alpha})")]
    public async Task<AppDataView> GetByNameAsync([FromRoute] string name)
    {
        var dataView = await _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Name, name)
            .FirstOrDefaultAsync();

        if (dataView == null) throw new NotFoundException();

        return dataView;
    }

    /// <summary>
    /// Get DataView by Id
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/[controller]({id:guid})/DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<IDataViewResponse> DataViewByIdAsync([FromRoute] Guid id, [FromBody] DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var dataView = await _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .In(x => x.Role, new[] { default(EntityRoleId?), Context.Role })
            .FirstOrDefaultAsync();

        return await RenderAsync(builder, dataView, request);
    }

    /// <summary>
    /// Get DataView by Name
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/[controller]/{name}/DataView")]
    [HttpPost("/api/v1/[controller]({name:alpha})/DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<IDataViewResponse> DataViewByNameAsync([FromRoute] string name, [FromBody] DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var dataView = await _connection.GetProfileElementAsync<AppDataView>(Context, name);
            
        return await RenderAsync(builder, dataView, request);
    }

    /// <summary>
    /// Render dataview 
    /// </summary>
    private async Task<IDataViewResponse> RenderAsync(ObjectDataViewBuilder builder, AppDataView dataView, DataViewRequest request)
    {
        request = Prepare(dataView, request);

        if (dataView == null)
        {
            return Error("View not found");
        }
            
        if (dataView.StoredProcedure?.Pipeline != null)
        {
            var response = await dataView.GetAsync(Context, _connection, request);
            response.Id = dataView.Id;
            return response;
        }

        var objectType = await _objectTypeService.GetAsync(Context, dataView.ObjectType);
        if (objectType == null)
        {
            return Error($"Object {dataView.ObjectType} not found");
        }

        return await builder.BuildDataViewAsync(Context, objectType, dataView, request);
            
        IDataViewResponse Error(string message)
        {
            return new DataViewResponse
            {
                View = dataView?.DataView ?? new DataView
                {
                    Name = "Error",
                    Fields = Array.Empty<FormField>(),
                },
                Request = request,
                Result = Enumerable.Empty<object>(),
                Message = message,
            }.UpdateFields();
        }            
    }

    /// <summary>
    /// Save DataView by Id
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/[controller]({id:guid})/DataView/Save")]
    [Produces("text/csv", "application/json")]
    public async Task<DataFormActionResponse> SaveDataViewByIdAsync([FromRoute] Guid id, [FromBody] SaveDataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var dataView = await _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .In(x => x.Role, new[] { default(EntityRoleId?), Context.Role })
            .FirstOrDefaultAsync();

        var objectType = await _objectTypeService.GetAsync(Context, dataView.ObjectType);
        return await builder.SaveDataViewAsync(Context, objectType, request);
    }

    /// <summary>
    /// Save DataView by Name
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/[controller]/{name}/DataView/Save")]
    [HttpPost("/api/v1/[controller]({name:alpha})/DataView/Save")]
    [Produces("text/csv", "application/json")]
    public async Task<DataFormActionResponse> SaveDataViewByNameAsync([FromRoute] string name, [FromBody] SaveDataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var dataView = await _connection.GetProfileElementAsync<AppDataView>(Context, name);
        var objectType = await _objectTypeService.GetAsync(Context, dataView.ObjectType);
        return await builder.SaveDataViewAsync(Context, objectType, request);
    }
        
    // /// <summary>
    // /// Get DataView for ObjectType
    // /// </summary>
    // [Authorize("managerplus")]
    // [HttpPost("ObjectType({id:guid})/DataView")]
    // [Produces("text/csv", "application/json")]
    // public async Task<IDataViewResponse> DataViewAsync(
    //     [FromRoute] Guid id,
    //     [FromBody] DataViewRequest request,
    //     [FromServices] UserActionService userActionService
    // )
    // {
    //     Prepare(request);
    //
    //     var response = await _objectTypeService.GetDataViewAsync(Context, id, request);
    //     // await userActionService.AddUserActionsAsync(Context, response.ObjectType, response);
    //     return response;
    // }
    //
    // /// <summary>
    // /// Get DataView for ObjectType
    // /// </summary>
    // [Authorize("managerplus")]
    // [HttpPost("ObjectType/{name}/DataView")]
    // [HttpPost("/api/v1/[controller]/ObjectType({name:alpha})/DataView")]
    // [Produces("text/csv", "application/json")]
    // public async Task<IDataViewResponse> DataViewByNameAsync(
    //     [FromRoute] string name,
    //     [FromBody] DataViewRequest request,
    //     [FromServices] ObjectTypeService objectTypeService,
    //     [FromServices] UserActionService userActionService
    // )
    // {
    //     Prepare(request);
    //
    //     var response = await _objectTypeService.GetDataViewAsync(Context, name, request);
    //     // await userActionService.AddUserActionsAsync(Context, response.ObjectType, response);
    //
    //     return response;
    // }
    //
    // /// <summary>
    // /// Import csv into collection of ObjectType
    // /// </summary>
    // [Authorize("managerplus")]
    // [HttpPost("ObjectType({id:guid})/Import")]
    // [Consumes("application/octet-stream", "multipart/form-data")]
    // public async Task<DataFormActionResponse> DataViewImportByIdAsync([FromRoute] Guid id, [FromForm] IFormFile file, ObjectTypeService objectTypeService)
    // {
    //     return await objectTypeService.ImportCsvAsync(Context, id, file.OpenReadStream());
    // }
    //
    // /// <summary>
    // /// Import csv into collection of ObjectType
    // /// </summary>
    // [Authorize("managerplus")]
    // [HttpPost("ObjectType/{name}/Import")]
    // [HttpPost("/api/v1/[controller]/ObjectType({name:alpha})/Import")]
    // [Consumes("application/octet-stream", "multipart/form-data")]
    // public async Task<DataFormActionResponse> DataViewImportByNameAsync([FromRoute] string name, [FromForm] IFormFile file, ObjectTypeService objectTypeService)
    // {
    //     return await objectTypeService.ImportCsvAsync(Context, name, file.OpenReadStream());
    // }
}