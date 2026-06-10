using System.Collections.Generic;

namespace Messages.Flow;

/// <summary>
/// Update object using data from flow run
/// </summary>
public class CreateObjectActionOptions : ActionOptions, IActionOptionsForRunner
{
    public const string ObjectCreatedEvent = nameof(ObjectCreatedEvent);
    public const string FailedToCreateObjectEvent = nameof(FailedToCreateObjectEvent);

    /// <summary>
    /// Object to be created
    /// </summary>
    public string ObjectType { get; set; }

    /// <summary>
    /// Update Operations
    /// </summary>
    public Dictionary<string, object> Mapping { get; set; }
    
    /// <summary>
    /// Alias to be used for the object created 
    /// </summary>
    public string Alias { get; set; }

    // public ObjectType BuildActionOptionsObjectType(IEntityContext context)
    // {
    //     var objectType = new ObjectType
    //     {
    //         Id = Guid.NewGuid(),
    //         Name = $"{nameof(ActionIds.CreateObject)}ActionOptions",
    //         Description = "Create Object Options",
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
    //             Description = "Object Type to be created.",
    //             IsRequired = true,
    //             ReferenceFieldOptions = new ReferenceFieldOptions
    //             {
    //                 ObjectType = nameof(ObjectType),
    //                 ForeignFieldName = nameof(PI.Shared.Models.ObjectType.Name),
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
    //         Name = nameof(ActionIds.CreateObject),
    //         Description = "Create Object",
    //         ActionId = ActionIds.CreateObject,
    //         InputObjectTypes = null,
    //         IconName = null,
    //         ActionOptionsObjectType = $"{nameof(ActionIds.CreateObject)}ActionOptions",
    //         Role = EntityRoleId.Admin,
    //         ProfileIds = null,
    //         Outputs = new Dictionary<string, string>
    //         {
    //             { ObjectCreatedEvent, "Object Created" },
    //             { FailedToCreateObjectEvent, "Failed to create Object" }
    //         }
    //     };
    //
    //     return action;
    // }
}