using System.Collections;
using Crochik.Mongo;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Diff;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Designer;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

public partial class ObjectTypeDesignerController
{
    private DiffResult GetDiff(ObjectType source, ObjectTypeDraft draft)
    {
        var diff = SimpleDiffer.Diff(source, draft.ObjectType, new SimpleDiffOptions
        {
            SkipBsonIgnore = true,
            ExcludeProperty = (type, info) =>
            {
                if (type == typeof(ObjectType))
                {
                    return info.Name switch
                    {
                        nameof(ObjectType.LoadedBaseObjectType) => true,
                        // nameof(ObjectType.RelatedObjectTypes) => true,
                        nameof(ObjectType.LastModifiedOn) => true,
                        nameof(ObjectType.LastActor) => true,
                        nameof(ObjectType.CreatedOn) => true,
                        nameof(ObjectType.OverriddenFields) => true,
                        _ => false,
                    };
                }

                return false;
            },
        });

        return diff;
    }

    /// <summary>
    /// Add profile access to object type
    /// </summary>
    [HttpGet("/api/v1/[controller]({objectTypeDraftId})/Discard/DataForm")]
    public async Task<Form> GetDiscardFormAsync([FromRoute] Guid objectTypeDraftId)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);
        var objectType = await _objectTypeService.GetAsync(Context, draft.Name, new GetObjectOptions
        {
            LoadBaseObject = false,
        });

        if (objectType == null) return Form.BuildErrorForm("This is a new object, no changes to discard");

        var diff = GetDiff(objectType, draft);

        return new Form
        {
            Title = "Discard",
            Fields =
            [
                new LabelField
                {
                    Name = "Message",
                    Label = "Are you sure you want to discard all the pending changes to this object?",
                },
                new TextField
                {
                    Name = "Pending Changes",
                    DefaultValue = diff?.ToChangeList() ?? "Nothing changed",
                    TextFieldOptions = new TextFieldOptions
                    {
                        Multline = true,
                    }
                }
            ],
            Actions =
            [
                new FormAction
                {
                    Name = "Discard",
                    Action = "Discard",
                },
                new FormAction
                {
                    Name = "Cancel",
                    Action = FormAction.Client_Cancel
                }
            ]
        };
    }

    /// <summary>
    /// Discard changes to Draft
    /// </summary>
    [HttpPost("/api/v1/[controller]({objectTypeDraftId})/Discard/DataForm")]
    public async Task<DataFormActionResponse> DiscardChangesAsync([FromRoute] Guid objectTypeDraftId, [FromBody] DataFormActionRequest request)
    {
        if (request.Action != "Discard")
        {
            return DataFormActionResponse.Error(request, $"Unexpected action: {request.Action}");
        }

        var draft = await GetOrCreateAsync(objectTypeDraftId: objectTypeDraftId);
        if (draft == null)
        {
            return DataFormActionResponse.Error(request, "Failed to reset it");
        }

        return new DataFormActionResponse(request, "Pending changes discarded", true);
    }

    /// <summary>
    /// Get "Apply" form with list of changes 
    /// </summary>
    [HttpGet("/api/v1/[controller]({objectTypeDraftId})/Apply/DataForm")]
    public async Task<Form> GetApplyChangesFormAsync([FromRoute] Guid objectTypeDraftId)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);
        var objectType = await _objectTypeService.GetAsync(Context, draft.Name, new GetObjectOptions
        {
            LoadBaseObject = false,
        });

        if (objectType == null)
        {
            return new Form
            {
                Title = "Apply Changes",
                Fields =
                [
                    new LabelField
                    {
                        Name = "Message",
                        Label = "This is a new object. Continuing will create it",
                    },
                ],
                Actions =
                [
                    new FormAction
                    {
                        Name = "Apply",
                        Label = "Create Object",
                        Action = "Apply",
                    },
                    new FormAction
                    {
                        Name = "Cancel",
                        Action = FormAction.Client_Cancel
                    }
                ]
            };            
        }
        
        var diff = GetDiff(objectType, draft);

        if (diff == null)
        {
            return Form.BuildErrorForm("Nothing has changed");
        }

        return new Form
        {
            Title = "Apply Changes",
            Fields =
            [
                new LabelField
                {
                    Name = "Message",
                    Label = "This action can't be reverted. Are you sure you want to apply pending changes to Object?",
                },
                new TextField
                {
                    Name = "Pending Changes",
                    DefaultValue = diff.ToChangeList(),
                    TextFieldOptions = new TextFieldOptions
                    {
                        Multline = true,
                    }
                }
            ],
            Actions =
            [
                new FormAction
                {
                    Name = "Apply",
                    Action = "Apply",
                },
                new FormAction
                {
                    Name = "Cancel",
                    Action = FormAction.Client_Cancel
                }
            ]
        };
    }

    /// <summary>
    /// Apply draft changes to the object type
    /// </summary>
    [HttpPost("/api/v1/[controller]({objectTypeDraftId})/Apply/DataForm")]
    public async Task<DataFormActionResponse> ApplyChangesAsync([FromRoute] Guid objectTypeDraftId, [FromBody] DataFormActionRequest request)
    {
        await CheckPermission(ObjectType.ObjectTypeFullName, ObjectTypePermission.Update | ObjectTypePermission.Read);
        
        if (request.Action != "Apply")
        {
            return DataFormActionResponse.Error(request, $"Unexpected action: {request.Action}");
        }

        var draft = await GetDraftAsync(objectTypeDraftId);
        var objectType = await _objectTypeService.GetAsync(Context, draft.Name, new GetObjectOptions
        {
            LoadBaseObject = false,
        });

        if (objectType == null)
        {
            // TODO: ....
            return DataFormActionResponse.Error(request, "Not implemented yet");
        }
        
        var diff = GetDiff(objectType, draft);

        if (diff == null) return DataFormActionResponse.Error(request, "Nothing to do");

        var before = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.FullName, draft.Name)
            .FirstOrDefaultAsync();

        var query = _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.FullName, draft.Name)
            .Update
            .Set(x => x.LastActor, Context.Actor)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow);

        diff.Traverse((path, name, t, o, n) =>
        {
            var full = string.Join(".", path.Append(name));

            if (t == DiffType.Unset)
            {
                query.Unset(full);
                return;
            }

            // special serializers 
            if (n is IDictionary dict)
            {
                var newDict = new Dictionary<string, object>();
                foreach (var key in dict.Keys)
                {
                    if (key == null) continue;
                    newDict[$"{key}"] = dict[key];
                }

                query.Set(full, newDict);
                return;
            }

            query.Set(full, n);
        }, []);

        var after = await query.UpdateAndGetOneAsync();

        await _connection.InsertAsync(new ObjectTypeHistory
        {
            Id = Model.NewGuid(),
            AccountId = Context.AccountId.Value,
            EntityId = Context.EntityId.Value,
            CreatedOn = DateTime.UtcNow,
            Before = before,
            After = after,
            LastActor = Context.Actor,
            Name = after.FullName,
        });

        // layouts
        if (draft.Layouts != null)
        {
            foreach (var kvp in draft.Layouts)
            {
                var layout = await _connection.InsertAsync(kvp.Value);

                // replace any other (active) layouts 
                var layoutQuery = _connection.Filter<AppFormLayout>()
                        .Eq(x => x.AccountId, layout.AccountId)
                        .Eq(x => x.ObjectType, layout.ObjectType)
                        .Eq(x => x.FormName, layout.FormName)
                        .Ne(x => x.Id, layout.Id)
                        .Ne(x => x.IsActive, false)
                    ;

                if (layout.ProfileIds?.Length > 0)
                {
                    layoutQuery.All(x => x.ProfileIds, layout.ProfileIds);
                }
                else if (layout.Role.HasValue)
                {
                    layoutQuery.Eq(x => x.Role, layout.Role);
                }

                await layoutQuery.Update
                    .Set(x => x.IsActive, false)
                    .Set(x => x.ReplacedById, layout.Id)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, Context.Actor)
                    .UpdateManyAsync();
            }
        }

        draft = await GetOrCreateAsync(objectTypeDraftId: objectTypeDraftId);
        if (draft == null)
        {
            return DataFormActionResponse.Error(request, "Failed to reset it");
        }

        return new DataFormActionResponse(request, "Pending changes applied", true);
    }
}