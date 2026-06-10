using System.Collections.Generic;

namespace Messages.Flow;

/// <summary>
/// Update object using data from flow run
/// </summary>
public class UpdateObjectActionOptions : ActionOptions, IActionOptionsForRunner
{
    public const string ObjectUpdatedEvent = nameof(ObjectUpdatedEvent);
    public const string FailedToUpdateObjectEvent = nameof(FailedToUpdateObjectEvent);
 
    /// <summary>
    /// Object to be created
    /// </summary>
    public string ObjectType { get; set; }
    
    /// <summary>
    /// Path to object in the flow run (e.g. {{InitialObject._id}} or {{Objects.Name._id}})
    /// if null, means current flow run target
    /// </summary>
    public string ObjectId { get; set; }

    /// <summary>
    /// Update Operations
    /// </summary>
    public Dictionary<string, object> Mapping { get; set; }
    
    // public ObjectType BuildActionOptionsObjectType(IEntityContext context)
    // {
    //     var objectType = new ObjectType
    //     {
    //         Id = Guid.NewGuid(),
    //         Name = $"{nameof(ActionIds.UpdateObject)}ActionOptions",
    //         Description = "Update Object Options",
    //         AccountId = context.AccountId.Value,
    //         EntityId = context.AccountId.Value,
    //         BaseObjectType = "ActionOptions",
    //         IsEmbedded = true,
    //         CollectionName = "*",
    //         NativeType = "?",
    //         RBAC = new ObjectTypeRBAC
    //         {
    //             [EntityRoleId.Admin] = ObjectTypePermission.Read | ObjectTypePermission.Update | ObjectTypePermission.Create | ObjectTypePermission.Delete,
    //         },
    //         Fields = getFields().Select(x => new FieldTemplate
    //         {
    //             Field = x,
    //             RBAC = new FieldRBAC()
    //             {
    //                 [EntityRoleId.Admin] = FieldPermission.Read | FieldPermission.Update | FieldPermission.SetOnCreate,
    //             }
    //         }).ToDictionary(x => x.Field.Name)
    //     };
    //
    //     return objectType;
    //
    //     IEnumerable<FormField> getFields()
    //     {
    //         yield return new ReferenceField
    //         {
    //             Name = nameof(ObjectType),
    //             Label = "Object Type",
    //             Description = "Object Type to be updated. If empty, it will be flow Target Object Type",
    //             IsRequired = false,
    //             ReferenceFieldOptions = new ReferenceFieldOptions
    //             {
    //                 ObjectType = nameof(ObjectType),
    //                 ForeignFieldName = nameof(PI.Shared.Models.ObjectType.Name),
    //             },
    //         };
    //         
    //         yield return new TextField
    //         {
    //             Name = nameof(ObjectId),
    //             Label = "Object Id (expression)",
    //             Description = "Object Id to be updated. If empty, it will be flow Target Object. It can be an Expression.",
    //             IsRequired = false,
    //             TextFieldOptions = new TextFieldOptions
    //             {
    //                 AllowExpressions = true,
    //             },
    //         };
    //
    //         yield return new DictionaryField
    //         {
    //             Name = nameof(Mapping),
    //             Label = "Fields",
    //             IsRequired = true,
    //             Description = "Fields to be modified. Value can be an Expression.",
    //             DictionaryFieldOptions = new DictionaryFieldOptions
    //             {
    //                 KeyField = new ReferenceField
    //                 {
    //                     Name = "FieldName",
    //                     ReferenceFieldOptions = new ReferenceFieldOptions
    //                     {
    //                         ObjectType = "/api/v1/CustomObject/{{ObjectType}}/Fields",
    //                     }
    //                 },
    //                 ValueField = new TextField
    //                 {
    //                     Name = "Expression",
    //                     TextFieldOptions = new TextFieldOptions
    //                     {
    //                         AllowExpressions = true
    //                     }
    //                 }
    //             },
    //             Visible = new [] { nameof(ObjectType) },
    //         };
    //     }
    // }
    //
    // public GenericAction BuildGenericAction(IEntityContext context)
    // {
    //     var action = new GenericAction
    //     {
    //         AccountId = context.AccountId.Value,
    //         CreatedOn = DateTime.UtcNow,
    //         Name = nameof(ActionIds.UpdateObject),
    //         Description = "Update Object",
    //         ActionId = ActionIds.UpdateObject,
    //         InputObjectTypes = null,
    //         IconName = null,
    //         ActionOptionsObjectType = $"{nameof(ActionIds.UpdateObject)}ActionOptions",
    //         Role = EntityRoleId.Admin,
    //         ProfileIds = null,
    //         Outputs = new Dictionary<string, string>
    //         {
    //             { ObjectUpdatedEvent, "Object Updated" },
    //             { FailedToUpdateObjectEvent, "Failed to update Object" }
    //         }
    //     };
    //
    //     return action;
    // }
}