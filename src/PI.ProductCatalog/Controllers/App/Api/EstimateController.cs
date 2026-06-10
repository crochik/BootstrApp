using System;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Models.MeasureSquare;
using PI.ProductCatalog.Services;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Requests;
using PI.Shared.Services;
using Services;

namespace Controllers;

[Authorize("rest")]
// [ApiExplorerSettings(IgnoreApi = true)]
[ApiExplorerSettings(GroupName = "rest")]
[Route("/productcatalog/api/[controller]")]
public class EstimateController(MongoConnection connection, ObjectTypeService objectTypeService, EstimateService service) : APIController
{
    /// <summary>
    /// Create estimate from list of room selections
    /// </summary>
    [HttpPost("RoomSelections")]
    [HttpPost] 
    [UseApiNames]
    public async Task<ExpandoObject> CreateFromSelectionsAsync([FromBody] EstimateService.CreateEstimateRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var result = await service.CreateEstimateAsync(Context, request);
        if (result.IsError) throw new BadRequestException(result.Status);

        var objectType = await objectTypeService.GetAsync(Context, result.Value.ObjectType);
        if (objectType == null) throw new NotFoundException();

        return await builder.GetObjectAsync(Context, objectType, result.Value.Id, useApiNames: true);
    }
    
    [HttpPost("/productcatalog/api/[controller]({objectId})/Duplicate/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> DuplicateAsync([FromBody] DataFormActionRequest request, [FromRoute] Guid objectId)
    {
        var load = await service.GetEstimateAsync(Context, objectId);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.DuplicateAsync(Context, load.Value);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);

        return new DataFormActionResponse(request, success: true)
        {
            Ids = [result.Value.Id],
        };
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    // [HttpPost("RemoveItem/DataForm")]
    [HttpPost("RemoveItem/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> RemoveItemAsync([FromBody] DataFormActionRequest<EstimateService.RemoveItemRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.RemoveItemAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Update Margin on all line items
    /// </summary>
    // [HttpPost("AddFreight/DataForm")]
    [HttpPost("BlendedMargin/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> BlendedMarginAsync([FromBody] DataFormActionRequest<EstimateService.BlendedMarginRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.UpdateBlendedMarginAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }
    
    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    [HttpPost("RemoveTaggedItems/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> RemoveTaggedItemAsync([FromBody] DataFormActionRequest<EstimateService.RemoveTaggedItemsRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var tags = load.Value.LineItems.SelectMany(x => x.Tags ?? []).Distinct().Order().ToArray();
        if (!tags.Contains(request.Parameters.Tag))
        {
            var msg = tags.Length switch
            {
                1 => $"Only valid Tag is \"{tags.First()}\"",
                _ => $"Valid Tags are {string.Join(", ", tags.SkipLast(1).Select(x => $"\"{x}\""))} & \"{tags.Last()}\"",
            };

            return DataFormActionResponse.Error(request, $"Tag \"{request.Parameters.Tag}\" not found. {msg}");
        }

        if (!string.IsNullOrWhiteSpace(request.Parameters.Name))
        {
            load.Value.Name = request.Parameters.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Parameters.Description))
        {
            load.Value.Description = request.Parameters.Description.Trim();
        }

        var result = await service.DuplicateAsync(Context, load.Value, removeTag: request.Parameters.Tag);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);

        return new DataFormActionResponse(request, success: true)
        {
            Ids = [result.Value.Id],
        };
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    // [HttpPost("AddItem/DataForm")]
    [HttpPost("AddItem/UserAction({eventId})/DataForm")]
    [HttpPost("/productcatalog/api/[controller]({objectId})/AddItem/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> AddItemAsync([FromBody] DataFormActionRequest<EstimateService.AddItemRequest> request, [FromQuery] Guid? objectId)
    {
        objectId ??= request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null;

        var load = await service.GetEstimateAsync(Context, objectId);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.AddItemAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);

        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    // [HttpPost("SwitchItem/DataForm")]
    [HttpPost("SwitchItem/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> SwitchItemAsync([FromBody] DataFormActionRequest<EstimateService.SwitchItemRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.SwitchItemAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    // [HttpPost("EditItem/DataForm")]
    [HttpPost("EditItem/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> EditItemAsync([FromBody] DataFormActionRequest<EstimateService.EditItemRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.EditItemAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    // [HttpPost("AddFreight/DataForm")]
    [HttpPost("AddFreight/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> AddFreightAsync([FromBody] DataFormActionRequest<EstimateService.AddFreightRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.AddItemAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    // [HttpPost("SetDiscount/DataForm")]
    [HttpPost("SetDiscount/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> SetDiscountAsync([FromBody] DataFormActionRequest<EstimateService.SetDiscountRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.SetDiscountAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    // [HttpPost("AddSection/DataForm")]
    [HttpPost("AddSection/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> AddSectionAsync([FromBody] DataFormActionRequest<EstimateService.AddSectionRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.AddSectionAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    // [HttpPost("EditSection/DataForm")]
    [HttpPost("EditSection/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> EditSectionAsync([FromBody] DataFormActionRequest<EstimateService.EditSectionRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.EditSectionAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    // [HttpPost("RemoveSection/DataForm")]
    [HttpPost("RemoveSection/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> RemoveSectionAsync([FromBody] DataFormActionRequest<EstimateService.RemoveSectionRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.RemoveSectionAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, "Section Edited", success: true);
    }

    /// <summary>
    /// Edit Info
    /// </summary>
    [HttpPost("Info/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> EditInfoAsync([FromBody] DataFormActionRequest<EstimateService.EditInfoRequest> request)
    {
        var load = await service.GetEstimateAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.UpdateInfoAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    // [AllowAnonymous]
    [HttpGet("/productcatalog/api/[controller]({id})/Seams")]
    public async Task<IActionResult> CalculateCarpetSeamsAsync([FromRoute] Guid id, [FromServices] MeasureSquareService measureSquareService)
    {
        var estimate = await connection.Filter<Estimate>()
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();
        if (estimate == null) return BadRequest("not found");

        var result = await measureSquareService.CalculateAsync(Context, estimate, new MeasureSquareService.SeamOptions
        {
            Direction = Direction.Auto,
            // RollWidthInches = 9 * 12,
        });
        
        if (result.IsError) return BadRequest(result.Status);

        return File(result.Value.Response.ImageBytes, "image/png");
    }
}