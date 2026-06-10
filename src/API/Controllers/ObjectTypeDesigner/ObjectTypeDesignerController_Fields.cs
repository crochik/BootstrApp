using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
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
    /// Get form to add field to object type 
    /// </summary>
    /// <param name="objectTypeDraftId"></param>
    /// <returns></returns>
    [HttpGet("/api/v1/[controller]({objectTypeDraftId})/Field/Add/DataForm")]
    public async Task<Form> GetAddFieldFormAsync([FromRoute] Guid objectTypeDraftId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, nameof(FieldTemplate));
        var form = await _objectTypeService.GetAddDataFormAsync(Context, objectType);

        form.Title = "New Field";
        form.Menu = null; // remove design, ...

        // TODO: set default permissions based on object type RBAC? 
        // ... 
        var permissions = form.Fields.FirstOrDefault(x => x.Name == $"{nameof(FieldTemplate.RBAC)}|{nameof(FieldRBAC.Permissions)}") as DictionaryField;
        if (permissions != null)
        {
            permissions.DefaultValue = new Dictionary<string, object>
            {
                [nameof(EntityRoleId.Admin)] = (int)(FieldPermission.Read | FieldPermission.Update | FieldPermission.SetOnCreate),
            };
        }

        return form;
    }

    /// <summary>
    /// process form for adding a field to   an object type
    /// </summary>
    [HttpPost("/api/v1/[controller]({objectTypeDraftId})/Field/Add/DataForm")]
    public async Task<DataFormActionResponse> AddFieldFormAsync([FromRoute] Guid objectTypeDraftId, [FromBody] DataFormActionRequest request)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Update);
        
        var objectType = await _objectTypeService.GetAsync(Context, nameof(FieldTemplate));

        var result = await _objectTypeService.GetFieldValuesFromUserInputAsync(Context, objectType, new ObjectTypeService.GetValuesFromInputOptions
            {
                Input = request.Parameters,
                ExcludeNulls = true,
            }
        );
        
        if (!result.IsSuccess) return new DataFormActionResponse(request, result.Status);

        if (!result.Value.TryGetValue(nameof(FieldTemplate.Field), out var fieldObj) || fieldObj is not IDictionary<string, object> field)
        {
            return DataFormActionResponse.Error(request, "Missing Field");
        }

        if (!field.TryGetStrParam(nameof(FormField.Name), out var name))
        {
            return DataFormActionResponse.Error(request, "Missing Required Field Name");
        }

        // TODO: check whether there is already a field with the same name 
        // ...
        
        var update = await UpdateQuery(objectTypeDraftId)
            .Set($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(objectType.Fields)}.{name}", result.Value)
            .UpdateAndGetOneAsync();

        if (update == null) return new DataFormActionResponse(request, "Failed to add field");

        return new DataFormActionResponse(request, "Field added")
        {
            NextUrl = FormAction.Client_Reload,
            Success = true,
        };
    }

    /// <summary>
    /// Get form to edit a field from an object type
    /// TODO: edit default, visible, enable, ...
    /// </summary>
    /// <param name="objectTypeDraftId"></param>
    /// <returns></returns>
    [HttpGet("/api/v1/[controller]({objectTypeDraftId})/Field({fieldName})/DataForm")]
    public async Task<Form> GetEditFieldFormAsync([FromRoute] Guid objectTypeDraftId, [FromRoute] string fieldName)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);
        if (!draft.ObjectType.Fields.TryGetValue(fieldName, out var field))
        {
            if (string.IsNullOrEmpty(draft.ObjectType.BaseObjectType)) throw NotFoundException.New("Field not found");
            
            var baseObjectType = await _objectTypeService.GetAsync(Context, draft.ObjectType.BaseObjectType);
            if (!baseObjectType.Fields.TryGetValue(fieldName, out field))
            {
                throw NotFoundException.New("Field not found in based object");
            }
        }

        var objectType = await _objectTypeService.GetAsync(Context, nameof(FieldTemplate));

        var record = ObjectTypeService.AsSerialized(field);
        var form = await _objectTypeService.GetEditDataFormAsync(Context, objectType, Guid.Empty, record, FormName.Edit);
        form.Actions =
        [
            new FormAction
            {
                Name = FormAction.Delete,
            },
            new FormAction
            {
                Name = FormAction.Update,
                Enable =
                [
                    Form.RequiredFieldsName
                ]
            }
        ];

        form.Title = field.Field.Label ?? field.Field.Name;
        form.Menu = null;

        return form;
    }

    /// <summary>
    /// Edit field in object type 
    /// </summary>
    [HttpPost("/api/v1/[controller]({objectTypeDraftId})/Field({fieldName})/DataForm")]
    public async Task<DataFormActionResponse> EddFieldFormAsync([FromRoute] Guid objectTypeDraftId, [FromRoute] string fieldName, [FromBody] DataFormActionRequest request)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Update | ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);

        if (!draft.ObjectType.Fields.TryGetValue(fieldName, out var field))
        {
            if (string.IsNullOrEmpty(draft.ObjectType.BaseObjectType)) throw NotFoundException.New("Field not found");
            
            var baseObjectType = await _objectTypeService.GetAsync(Context, draft.ObjectType.BaseObjectType);
            if (!baseObjectType.Fields.TryGetValue(fieldName, out field))
            {
                throw NotFoundException.New("Field not found in based object");
            }
        }

        // var objectType = await _objectTypeService.GetAsync(Context, field.Field.GetType().Name);
        // if (objectType == null) throw NotFoundException.New("Field type not found");
        var objectType = await _objectTypeService.GetAsync(Context, nameof(FieldTemplate));

        // TODO: there must be a better way :)
        // ... 
        var query = _connection.Filter<ExpandoObject>("ObjectType.Draft")
            .Eq(nameof(ObjectTypeDraft.AccountId), Context.AccountId)
            .Eq(Model.IdFieldName, objectTypeDraftId)
            .Update
            .Set(nameof(ObjectTypeDraft.LastModifiedOn), DateTime.UtcNow);

        if (request.Action == FormAction.Delete)
        {
            // delete field
            query.Unset($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.Fields)}.{fieldName}");
        }
        else
        {
            // update field
            // var input = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(field.Field));
            var result = await _objectTypeService.GetFieldValuesFromUserInputAsync(Context, objectType, new ObjectTypeService.GetValuesFromInputOptions
                {
                    Input = request.Parameters,
                    ExcludeNulls = true,
                }
            );
            
            if (!result.IsSuccess) return new DataFormActionResponse(request, result.Status);

            if (!result.Value.TryGetValue(nameof(FieldTemplate.Field), out var fieldObj) || fieldObj is not IDictionary<string, object> fieldValue)
            {
                return DataFormActionResponse.Error(request, "Couldn't find Field");
            }

            if (!fieldValue.TryGetStrParam(nameof(FormField.Name), out var newFieldName))
            {
                return DataFormActionResponse.Error(request, "Couldn't find Field Name");
            }

            // check if the name was changed, if so it has to change the key as well
            if (!string.Equals(fieldName, newFieldName, StringComparison.InvariantCulture))
            {
                query.Unset($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.Fields)}.{fieldName}");
                fieldName = newFieldName;
            }

            query.Set($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.Fields)}.{fieldName}", result.Value);
        }

        var update = await query.UpdateAndGetOneAsync();
        if (update == null) return new DataFormActionResponse(request, "Failed to update field");

        return new DataFormActionResponse(request, "Field updated")
        {
            NextUrl = FormAction.Client_Reload,
            Success = true,
        };
    }

}