using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;
using Services;

namespace Controllers;

[Authorize("rest")]
[ApiExplorerSettings(GroupName = "rest")]
[Route("/productcatalog/api/[controller]")]
public class RoomSelectionController(ILogger<RoomSelectionController> logger, MongoConnection connection, ObjectTypeService objectTypeService, RoomSelectionFormInterceptor interceptor, EstimateService service) : APIController
{
    /// <summary>
    /// Create room selection
    /// </summary>
    [HttpPost]
    [UseApiNames]
    public async Task<ExpandoObject> CreateAsync([FromBody] CreateRoomSelectionRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        Result<RoomSelection> result = await service.CreateRoomSelectionAsync(Context, templateId: request.TemplateId, itemId: request.ItemId);
        if (result.IsError) throw new BadRequestException(result.Status);
        if (result.IsUnknown) throw new NotFoundException(result.Status);

        var objectType = await objectTypeService.GetAsync(Context, result.Value.ObjectType);
        if (objectType == null) throw new NotFoundException();

        return await builder.GetObjectAsync(Context, objectType, result.Value.Id, useApiNames: true);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    [HttpPost("RemoveItem/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> RemoveItemAsync([FromBody] DataFormActionRequest<EstimateService.RemoveItemRequest> request)
    {
        return await BatchProcessAsync(request.Parameters.AppliesTo, request, s => service.RemoveItemAsync(Context, s, request.Parameters));
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    [HttpPost("RemoveTaggedItems/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> RemoveTaggedItemAsync([FromBody] DataFormActionRequest<EstimateService.RemoveTaggedItemsRequest> request)
    {
        var load = await service.GetRoomSelectionAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
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
    [HttpPost("AddItem/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> AddItemAsync([FromBody] DataFormActionRequest<EstimateService.AddItemRequest> request)
    {
        return await BatchProcessAsync(request.Parameters.AppliesTo, request, s => service.AddItemAsync(Context, s, request.Parameters));
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    [HttpPost("/productcatalog/api/[controller]({objectId})/AddItem/UserAction({eventId})")]
    [UseApiNames]
    public async Task<DataFormActionResponse> AddItemAsync([FromBody] EstimateService.AddItemRequest body, [FromRoute] Guid objectId)
    {
        var request = new DataFormActionRequest<EstimateService.AddItemRequest>
        {
            Action = "AddItem",
            SelectedIds = [objectId],
            Parameters = body,
        };

        return await BatchProcessAsync(EstimateService.AppliesTo.Estimate, request, s => service.AddItemAsync(Context, s, request.Parameters));
    }

    [HttpPost("/productcatalog/api/[controller]({objectId})/Duplicate/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> DuplicateAsync([FromBody] DataFormActionRequest request, [FromRoute] Guid objectId)
    {
        var load = await service.GetRoomSelectionAsync(Context, objectId);
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
    [HttpPost("SwitchItem/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> SwitchItemAsync([FromBody] DataFormActionRequest<EstimateService.SwitchItemRequest> request)
    {
        var load = await service.GetRoomSelectionAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.SwitchItemAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    [HttpPost("EditItem/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> EditItemAsync([FromBody] DataFormActionRequest<EstimateService.EditItemRequest> request)
    {
        var load = await service.GetRoomSelectionAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.EditItemAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Execute Action on named Form (for object)
    /// </summary>
    [HttpPost("AddFreight/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> AddFreightAsync([FromBody] DataFormActionRequest<EstimateService.AddFreightRequest> request)
    {
        var load = await service.GetRoomSelectionAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.AddItemAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Update Margin on all line items
    /// </summary>
    [HttpPost("BlendedMargin/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> BlendedMarginAsync([FromBody] DataFormActionRequest<EstimateService.BlendedMarginRequest> request)
    {
        var load = await service.GetRoomSelectionAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.UpdateBlendedMarginAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Edit Info
    /// </summary>
    [HttpPost("Info/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> EditInfoAsync([FromBody] DataFormActionRequest<EstimateService.EditInfoRequest> request)
    {
        var load = await service.GetRoomSelectionAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        var result = await service.UpdateInfoAsync(Context, load.Value, request.Parameters);
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);
        return new DataFormActionResponse(request, success: true);
    }

    private async Task<DataFormActionResponse> BatchProcessAsync<T>(EstimateService.AppliesTo appliesTo, DataFormActionRequest<T> request, Func<RoomSelection, Task<IResult>> operation)
    {
        var load = await service.GetRoomSelectionAsync(Context, request.SelectedIds?.Length == 1 ? request.SelectedIds[0] : null);
        if (load.IsError) return DataFormActionResponse.Error(request, load.Status);

        RoomSelection selection = load.Value;
        var selections = await GetRoomSelections(appliesTo, selection);
        var modified = new List<Guid>();
        var errors = new List<string>();
        foreach (var s in selections)
        {
            var result = await operation(s);
            if (result.IsError)
            {
                logger.LogError("Failed for {SelectionId}: {Status}", s.Id, result.Status);
                errors.Add(result.Status);
            }

            if (result.IsSuccess)
            {
                modified.Add(s.Id);
            }
        }

        logger.LogInformation("Modified {SelectionIds}", string.Join(", ", modified));
        request.SelectedIds = modified.ToArray();

        if (errors.IsEmpty()) return new DataFormActionResponse(request, success: true);
        if (errors.Count == 1) return DataFormActionResponse.Error(request, errors.First());
        return DataFormActionResponse.Error(request, string.Join("; ", errors));
    }

    private async Task<List<RoomSelection>> GetRoomSelections(EstimateService.AppliesTo appliesTo, RoomSelection selection)
    {
        List<RoomSelection> selections = appliesTo switch
        {
            EstimateService.AppliesTo.ProductType => await connection.Filter<RoomSelection>()
                .Eq(x => x.AccountId, selection.AccountId)
                .Eq(x => x.Hash, selection.Hash)
                .Eq(x => x.ProductType, selection.ProductType)
                .Ne(x => x.IsActive, false)
                .FindAsync(),

            EstimateService.AppliesTo.All => await connection.Filter<RoomSelection>()
                .Eq(x => x.AccountId, selection.AccountId)
                .Eq(x => x.Hash, selection.Hash)
                .Ne(x => x.IsActive, false)
                .FindAsync(),

            _ => [selection],
        };
        return selections;
    }
    
    public class CreateRoomSelectionRequest
    {
        public Guid ItemId { get; set; }
        public Guid TemplateId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public string SessionKey { get; set; }
        public Guid[] RoomIds { get; set; }
        public string ProjectExternalId { get; set; }
        public string Hash => RoomSelection.CalculateHash(SessionKey, RoomIds);
    }
}