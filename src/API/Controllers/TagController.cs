using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Authorize("default")]
[Route("/api/v1/[controller]")]
public class TagController : APIController
{
    private readonly ObjectTypeService _objectTypeService;

    public TagController(ObjectTypeService objectTypeService)
    {
        _objectTypeService = objectTypeService;
    }

    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Lookup")]
    public async Task<IEnumerable<ReferenceValue>> LookupByIdAsync([FromRoute] string objectTypeName, DataViewRequest request, [FromServices] ObjectTypeService objectTypeService)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException(objectTypeName);

        return await objectTypeService.LookupTagsAsync(Context, objectType, request);
    }

    [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/DataForm")]
    public Form GetBulkTagForm([FromRoute] string objectTypeName)
    {
        return new Form
        {
            Name = $"{objectTypeName}_Tags",
            Title = "Tag/Untag...",
            Fields = new FormField[]
            {
                new TagsField
                {
                    Name = "Tags",
                    IsRequired = true,
                    TagFieldOptions = new TagsFieldOptions
                    {
                        Url = $"/api/v1/CustomObject/Tags({objectTypeName})"
                    }
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Untag",
                    Label = "Remove Tags",
                },
                new FormAction
                {
                    Name = "Tag",
                    Label = "Add Tags"
                }
            }
        };
    }

    /// <summary>
    /// Bulk edit tags
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/DataForm")]
    public async Task<DataFormActionResponse> BulkTagAsync([FromRoute] string objectTypeName, [FromBody] DataFormActionRequest request)
    {
        if ((request?.SelectedIds == null || request.SelectedIds.IsEmpty()) && string.IsNullOrEmpty(request.View))
        {
            return new DataFormActionResponse
            {
                Success = false,
                Message = "Missing selection"
            };
        }

        if (!request.Parameters.TryGetValue("Tags", out var tagsValue) || tagsValue is not IEnumerable enumerable)
        {
            return new DataFormActionResponse
            {
                Success = false,
                Message = "Missing tags"
            };
        }

        var tags = enumerable.ToEnumerableObject()
            .Select(x => x?.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (!string.IsNullOrEmpty(request.View))
        {
            return new DataFormActionResponse
            {
                Success = false,
                Message = "View support not implemented (yet)"
            };
        }

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException(objectTypeName);

        var remove = false;
        switch (request.Action)
        {
            case "Tag":
                break;
            
            case "Untag":
                remove = true;
                break;
            
            default:
                return new DataFormActionResponse
                {
                    Success = false,
                    Message = $"Invalid action {request.Action}"
                };
        }

        var changedIds = await _objectTypeService.BulkTagAsync(Context, objectType, request.SelectedIds, tags, remove);
        if (changedIds == null)
        {
            return new DataFormActionResponse
            {
                Success = true,
                Message = "Failed"
            };
        }

        return new DataFormActionResponse
        {
            Success = true,
            Ids = changedIds,
            Message = changedIds.IsEmpty() ? "No changes needed" : (remove ? "Tag(s) removed" : "Tagged")
        };
    }
}