using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Mvc;
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
    [HttpGet("/api/v1/[controller]({objectTypeDraftId})/RelatedObjectType/Add/DataForm")]
    public async Task<Form> GetAddRelatedObjectFormAsync([FromRoute] Guid objectTypeDraftId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, nameof(RelatedObjectType));
        var form = await _objectTypeService.GetAddDataFormAsync(Context, objectType);

        form.Title = "New Relation";
        form.Menu = null; // remove design, ...

        // // TODO: set default permissions based on object type RBAC? 
        // // ... 
        // var permissions = form.Fields.FirstOrDefault(x => x.Name == $"{nameof(FieldTemplate.RBAC)}|{nameof(FieldRBAC.Permissions)}") as DictionaryField;
        // if (permissions != null)
        // {
        //     permissions.DefaultValue = new Dictionary<string, object>
        //     {
        //         [nameof(EntityRoleId.Admin)] = (int)(FieldPermission.Read | FieldPermission.Update | FieldPermission.SetOnCreate),
        //     };
        // }

        return form;
    }

    /// <summary>
    /// process form for adding a field to   an object type
    /// </summary>
    [HttpPost("/api/v1/[controller]({objectTypeDraftId})/RelatedObjectType/Add/DataForm")]
    public async Task<DataFormActionResponse> AddRelatedObjectTypeAsync([FromRoute] Guid objectTypeDraftId, [FromBody] DataFormActionRequest request)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Update | ObjectTypePermission.Read);
        
        var objectType = await _objectTypeService.GetAsync(Context, nameof(RelatedObjectType));

        var result = await _objectTypeService.GetFieldValuesFromUserInputAsync(Context, objectType, new ObjectTypeService.GetValuesFromInputOptions
            {
                Input = request.Parameters,
                ExcludeNulls = true,
            }
        );
        
        if (!result.IsSuccess) return new DataFormActionResponse(request, result.Status);

        var update = await UpdateQuery(objectTypeDraftId)
            .Push($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(objectType.RelatedObjectTypes)}", result.Value)
            .UpdateAndGetOneAsync();
        
        if (update == null) return new DataFormActionResponse(request, "Failed to add related object type.");
        
        return new DataFormActionResponse(request, "Related Object Type added")
        {
            NextUrl = FormAction.Client_Reload,
            Success = true,
        };
    }


    [HttpGet("/api/v1/[controller]({objectTypeDraftId})/RelatedObjectType({name})/DataForm")]
    public async Task<Form> GetEditRelatedObjectTypeFormAsync([FromRoute] Guid objectTypeDraftId, [FromRoute] string name)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);

        var relation = draft.ObjectType.RelatedObjectTypes?.FirstOrDefault(x =>
            x.RelationType is RelationType.OneToOne or RelationType.OneToMany &&
            x.Name == name
        );

        if (relation == null)
        {
            return Form.BuildErrorForm("Invalid Relation Index");
        }

        var objectType = await _objectTypeService.GetAsync(Context, nameof(RelatedObjectType));

        var record = ObjectTypeService.AsSerialized(relation);
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

        form.Title = relation.Name;
        form.Menu = null;

        return form;
    }

    /// <summary>
    /// Edit field in object type 
    /// </summary>
    [HttpPost("/api/v1/[controller]({objectTypeDraftId})/RelatedObjectType({name})/DataForm")]
    public async Task<DataFormActionResponse> EditRelatedObjectTypeAsync([FromRoute] Guid objectTypeDraftId, [FromRoute] string name, [FromBody] DataFormActionRequest request)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Update | ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);

        var index = 0;
        for (; index < draft.ObjectType.RelatedObjectTypes.Length; index++)
        {
            var x = draft.ObjectType.RelatedObjectTypes[index];
            if (x.RelationType is RelationType.OneToOne or RelationType.OneToMany && x.Name == name) break;
        }

        if (index >= draft.ObjectType.RelatedObjectTypes.Length)
        {
            return DataFormActionResponse.Error(request, "Invalid Relation Index");
        }

        var objectType = await _objectTypeService.GetAsync(Context, nameof(RelatedObjectType));

        // TODO: there must be a better way :)
        // ... 
        var query = _connection.Filter<ExpandoObject>("ObjectType.Draft")
            .Eq(nameof(ObjectTypeDraft.AccountId), Context.AccountId)
            .Eq(Model.IdFieldName, objectTypeDraftId)
            .Update
            .Set(nameof(ObjectTypeDraft.LastModifiedOn), DateTime.UtcNow);

        if (request.Action == FormAction.Delete)
        {
            // delete relation
            var relatedObjectTypes = draft.ObjectType.RelatedObjectTypes.Where((x, i) => i != index).ToArray();
            query.Set($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.RelatedObjectTypes)}", relatedObjectTypes);
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

            if (!result.Value.TryGetStrParam(nameof(RelatedObjectType.Name), out var newName))
            {
                return DataFormActionResponse.Error(request, "Couldn't find Field Name");
            }
            
            query.Set($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.RelatedObjectTypes)}.{index}", result.Value);
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