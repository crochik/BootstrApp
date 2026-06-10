using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Designer;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

public partial class ObjectTypeDesignerController
{
    /// <summary>
    /// Preview form for object / profile / type 
    /// </summary>
    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]({objectTypeDraftId})/Profile({profileOrRole})/Form({formName})/DataForm")]
    public async Task<Form> GetFormForObjectTypeAsync([FromRoute] Guid objectTypeDraftId, [FromRoute] string profileOrRole, [FromRoute] FormName formName)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);
        var (context, profile) = await BuildContextAsync(profileOrRole);

        // complete fields 
        var baseObjectType = !string.IsNullOrEmpty(draft.ObjectType.BaseObjectType) ? await _objectTypeService.GetAsync(Context, draft.ObjectType.BaseObjectType) : null;
        if (baseObjectType != null)
        {
            foreach (var field in draft.ObjectType.Fields)
            {
                if (baseObjectType.Fields.TryGetValue(field.Key, out var baseField))
                {
                    ObjectTypeService.MergeField(field.Value, baseField);
                }
            }

            // add base fields that haven't been overridden 
            foreach (var field in baseObjectType.Fields)
            {
                draft.ObjectType.Fields.TryAdd(field.Key, field.Value);
            }
        }
        
        var existingLayout = draft.Layouts?.Values.FirstOrDefault(x =>
            x.FormName == formName.ToString() &&
            profile switch
            {
                null => x.Role.HasValue switch
                {
                    true => x.Role.Value.ToString() == profileOrRole,
                    _ => false,
                },
                _ => x.ProfileIds?.Contains(profile.Id) ?? false,
            }
        );

        var form = formName switch
        {
            FormName.Add => await _objectTypeService.GetAddDataFormAsync(context, draft.ObjectType, new GetFormOptions
            {
                // do not try to load a custom form for the profile/role
                SkipLoadingCustomForm = true,
                LoadLayout = existingLayout == null,
            }),
            _ => await _objectTypeService.GetEditDataFormAsync(context, draft.ObjectType, Guid.Empty, draft.Example, formName, new GetFormOptions
            {
                // do not try to load a custom form for the profile/role
                SkipLoadingCustomForm = true,
                LoadLayout = existingLayout == null,
            }),
        };

        if (existingLayout != null)
        {
            form.Layouts = existingLayout.Layouts;
        }

        if (form.Actions != null)
        {
            foreach (var action in form.Actions)
            {
                action.Enable = ["false"];
                action.Visible = null;
            }
        }

        form.Title = $"{draft.Name}({formName}) for {profile?.Name ?? profileOrRole}";

        if (formName == FormName.Add || formName == FormName.Edit)
        {
            form.Actions = form.Actions?
                .Append(new FormAction
                {
                    Name = "Save",
                    Action = "Save",
                    Label = "Update Example"
                })
                .ToArray();
        }

        form.Menu = new Menu
        {
            Name = "Form",
            Label = "Popup",
            Items =
            [
                new ActionMenuItem
                {
                    Icon = nameof(Icons.Design),
                    Name = FormAction.Client_Design,
                    Label = "Layout",
                    Action = FormAction.Client_Design,
                }
            ]
        };

        return form;
    }

    /// <summary>
    /// Handle actions from preview forms 
    /// </summary>
    [Authorize("admin")]
    [HttpPost("/api/v1/[controller]({objectTypeDraftId})/Profile({profileOrRole})/Form({formName})/DataForm")]
    public async Task<DataFormActionResponse> HandleFormForObjectTypeAsync([FromRoute] Guid objectTypeDraftId, [FromRoute] string profileOrRole, [FromRoute] FormName formName, [FromBody] DataFormActionRequest request)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Update | ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);

        var objectType = await _objectTypeService.GetAsync(Context, draft.Name);
        if (objectType == null) throw NotFoundException.New($"{draft.Name} not found");

        var (context, profile) = await BuildContextAsync(profileOrRole);

        // TODO: apply on-going changes from draft to object type  
        // ...

        var result = await _objectTypeService.GetFieldValuesFromUserInputAsync(context, objectType, new ObjectTypeService.GetValuesFromInputOptions
            {
                Input = request.Parameters,
                ExcludeNulls = true,
            }
        );

        if (!result.IsSuccess)
        {
            return new DataFormActionResponse(request, result.Status);
        }

        var update = await UpdateQuery(objectTypeDraftId)
            .Set(nameof(ObjectTypeDraft.Example), result.Value)
            .UpdateAndGetOneAsync();

        if (update == null)
        {
            return new DataFormActionResponse(request, "Failed to update draft");
        }

        return new DataFormActionResponse(request, "Example updated", true);
    }

    /// <summary>
    /// Get "Save layouts" for form (id in query or as part of the route) 
    /// </summary>
    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]({objectTypeDraftId})/Profile({profileOrRole})/Form({formName})/Layout/Save/DataForm")]
    public async Task<Form> GetSaveLayoutFormAsync([FromRoute] Guid objectTypeDraftId, [FromRoute] string profileOrRole, [FromRoute] FormName formName)
    // [FromRoute] string objectTypeName, [FromRoute] FormName formName = FormName.Edit)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);

        var objectType = await _objectTypeService.GetAsync(Context, draft.Name);
        if (objectType == null) throw NotFoundException.New($"{draft.Name} not found");

        var (context, profile) = await BuildContextAsync(profileOrRole);

        return new Form
        {
            Name = "SaveLayouts",
            Title = "Save Layouts",
            Fields = getFields().ToArray(),
            Actions =
            [
                new FormAction
                {
                    Name = FormAction.Client_Cancel,
                    Label = "Cancel",
                    Action = FormAction.Client_Cancel,
                },
                new FormAction
                {
                    Name = FormAction.Client_Save,
                    Label = "Save",
                    Action = FormAction.Client_Save,
                }
            ]
        };

        IEnumerable<FormField> getFields()
        {
            yield return new ReferenceField
            {
                Name = nameof(AppFormLayout.ObjectType),
                Label = "Object Type",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(PI.Shared.Models.ObjectType),
                    ForeignFieldName = nameof(PI.Shared.Models.ObjectType.Name),
                },
                DefaultValue = objectType.Name,
            };

            yield return new MultiReferenceField
            {
                Name = nameof(AppProfileElement.ProfileIds),
                Label = "Profile",
                MultiReferenceFieldOptions = new MultiReferenceFieldOptions
                {
                    ObjectType = nameof(AppProfile),
                },
                DefaultValue = profile != null ? new Guid[] { profile.Id } : null,
                Visible = [$"!{nameof(AppProfileElement.Role)}"],
            };

            yield return new SelectField
            {
                Name = nameof(AppProfileElement.Role),
                Label = "User Role",
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = new Dictionary<string, string>
                    {
                        { nameof(EntityRoleId.Admin), nameof(EntityRoleId.Admin) },
                        { nameof(EntityRoleId.Manager), nameof(EntityRoleId.Manager) },
                        { nameof(EntityRoleId.User), nameof(EntityRoleId.User) },
                    }
                },
                DefaultValue = profile == null ? context.Role : null,
                Visible = [$"!{nameof(AppProfileElement.ProfileIds)}"],
            };
        }
    }

    [Authorize("admin")]
    [HttpPost("/api/v1/[controller]({objectTypeDraftId})/Profile({profileOrRole})/Form({formName})/Layout/Save")]
    public async Task<BreakpointLayouts> SaveLayoutAsync([FromRoute] Guid objectTypeDraftId, [FromRoute] string profileOrRole, [FromRoute] FormName formName, [FromBody] SaveFormLayoutsRequest request)
    // [FromRoute] string objectTypeName, [FromBody] SaveFormLayoutsRequest request, [FromRoute] FormName formName = FormName.Edit)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Update | ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);

        var objectType = await _objectTypeService.GetAsync(Context, draft.Name);
        if (objectType == null) throw NotFoundException.New($"{draft.Name} not found");

        // var (context, profile) = await BuildContextAsync(profileOrRole);

        foreach (var breakPointLayout in request.Layouts.All.OfType<GridFormLayout>())
        {
            foreach (var row in breakPointLayout.Rows)
            {
                foreach (var cell in row.Fields)
                {
                    cell.Width = 12 / row.Fields.Length;
                }
            }
        }

        var layout = new AppFormLayout
        {
            AccountId = Context.AccountId.Value,
            CreatedOn = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            LastActor = Context.Actor,
            Layouts = request.Layouts,
            Name = request.Name ?? $"{objectType.Name}: {formName}",
            Description = request.Description ?? $"{formName} for {objectType.Name}",
            ProfileIds = request.ProfileIds,
            Role = request.Role,
            ObjectType = objectType.Name,
            FormName = formName.ToString(),
            IsActive = true,
        };

        if (request.ProfileIds != null)
        {
            Array.Sort(request.ProfileIds);
        }

        var key = request.Role.HasValue ? request.Role.ToString() : string.Join(",", request.ProfileIds);

        await _connection.Filter<ObjectTypeDraft>()
            .Eq(x => x.Id, objectTypeDraftId)
            .Update
            .Set(x => x.Layouts[$"{key}|{formName}"], layout)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, Context.Actor)
            .UpdateOneAsync();

        // TODO: fire event 
        // ...

        return layout.Layouts;
    }
}