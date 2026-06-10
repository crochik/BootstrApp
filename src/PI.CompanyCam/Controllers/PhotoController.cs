using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Mvc;
using PI.CompanyCam.Models;
using PI.CompanyCam.Services;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace PI.CompanyCam.Controllers;

[Route("/companycam/v1/[controller]")]
public class PhotoController : APIController
{
    private readonly CompanyCamService _service;
    private readonly ObjectTypeService _objectTypeService;
    private readonly MongoConnection _connection;

    public PhotoController(CompanyCamService service, ObjectTypeService objectTypeService, MongoConnection connection)
    {
        _service = service;
        _objectTypeService = objectTypeService;
        _connection = connection;
    }

    [HttpPost("{objectTypeName}({objectId})/DataView")]
    public async Task<IDataViewResponse> DataViewAsync([FromRoute] string objectId, [FromRoute] string objectTypeName, [FromBody] DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        if (!Model.TryParseGuid(objectId, out var id)) throw new BadRequestException("Invalid Object Id");

        var project = await _service.GetAssociatedProjectIdAsync(Context, objectTypeName, id);
        if (!project.IsSuccess) throw new BadRequestException(project.Status);

        return await BuildDataViewResponseAsync(builder, project.Value, request);
    }

    [HttpPost("/companycam/v1/Project({projectId})/[controller]/DataView")]
    public async Task<IDataViewResponse> DataViewAsync([FromRoute] string projectId, [FromBody] DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        return await BuildDataViewResponseAsync(builder, projectId, request);
    }

    private async Task<IDataViewResponse> BuildDataViewResponseAsync(ObjectDataViewBuilder builder, string projectId, DataViewRequest request)
    {
        var query = _connection.Filter<CCProject>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.ExternalId, projectId);

        if (Context.OrganizationId.HasValue)
        {
            query.Eq(x => x.EntityId, Context.OrganizationId);
        }
        
        var project = await query.FirstOrDefaultAsync();

        request.Fields =
        [
            "Thumbnail",
            "OriginalImage"
        ];

        request.FixedFields =
        [
            "Properties|project_id"
        ];

        request.Criteria = (request.Criteria ?? Enumerable.Empty<Condition>())
            .Where(x => x.FieldName switch
            {
                "Properties|project_id" => false,
                _ => true,
            })
            .Append(Condition.Eq("Properties|project_id", projectId))
            .ToArray();

        var objectType = await _objectTypeService.GetAsync(Context, "companycam.Photo");
        var dvr = await builder.BuildDataViewAsync(Context, objectType, request);

        // dvr.View.Fields = dvr.View.Fields
        //     .Where(x => x.IsVisible)
        //     .ToArray();

        // var dict = dvr.View.Fields.ToDictionary(x => x.Name);
        // dict["_id"].Visible = new[] { "false" };
        // dict["Properties|project_id"].Visible = new[] { "false" };

        if (project.Properties.TryGetValue("name", out var name) && name != null)
        {
            dvr.View.Title = name.ToString();
        }

        // dvr.View.Searchable = false;
        // dvr.View.IsSelectable = false;
        // dvr.View.IsFilterableLocally = false;
        // dvr.View.Filter = null; // disable filter/search
        // dvr.View.FilterForm = null; // disable filter form

        dvr.Options = new ImageGalleryViewOptions
        {
            ThumbnailUrl = "Thumbnail",
            ImageUrl = "OriginalImage",
            HideToolbar = true
        };

        return dvr;
    }
}