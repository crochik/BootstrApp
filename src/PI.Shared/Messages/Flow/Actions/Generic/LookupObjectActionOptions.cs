using System;
using System.Collections.Generic;
using PI.Shared.Models;

namespace Messages.Flow;

/// <summary>
/// Used to lookup for objects 
/// </summary>
public class LookupObjectActionOptions : ActionOptions, IActionOptionsForRunner
{
    public const string ObjectFoundEvent = nameof(ObjectFoundEvent);
    public const string ObjectNotFoundEvent = nameof(ObjectNotFoundEvent);
    public const string MoreThanOneObjectFoundEvent = nameof(MoreThanOneObjectFoundEvent);
    
    public string ObjectType { get; set; }
    public Criteria Criteria { get; set; }
    
    public string OrderBy { get; set; }
    public bool ReverseOrder { get; set; }
    
    /// <summary>
    /// When defined, determines the name of the object added to the FlowRun.Objects.{Nick}
    /// </summary>
    public string ObjectNickname { get; set; }
    
    // public ObjectType BuildActionOptionsObjectType(IEntityContext context)
    // {
    //     var objectType = new ObjectType
    //     {
    //         Id = Guid.NewGuid(),
    //         Name = $"{nameof(ActionIds.LookupObject)}ActionOptions",
    //         Description = "Lookup Object Options",
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
    //         yield return new ObjectField
    //         {
    //             Name = nameof(Criteria),
    //             Label = "Fields",
    //             IsRequired = true,
    //             Description = "Fields to be modified. Value can be an Expression.",
    //             ObjectFieldOptions = new ObjectFieldOptions
    //             {
    //                 ObjectType = nameof(PI.Shared.Models.Criteria)
    //             }
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
    //         Name = nameof(ActionIds.LookupObject),
    //         Description = "Lookup Object",
    //         ActionId = ActionIds.LookupObject,
    //         InputObjectTypes = null,
    //         IconName = null,
    //         ActionOptionsObjectType = $"{nameof(ActionIds.LookupObject)}ActionOptions",
    //         Role = EntityRoleId.Admin,
    //         ProfileIds = null,
    //         Outputs = new Dictionary<string, string>
    //         {
    //             { ObjectFoundEvent, "Object found" },
    //             { ObjectNotFoundEvent, "Object not found" },
    //             { MoreThanOneObjectFoundEvent, "More than one object found" },
    //         }
    //     };
    //
    //     return action;
    // }
}

/// <summary>
/// Find matching objects and fire event for each
/// NOT IMPLEMENTED YET
/// </summary>
public class BatchProcessActionOptions : ActionOptions, IActionOptionsForRunner
{
    public const string ObjectNotFoundEventName = "NoObjectsFound";
    public const string ErrorEventName = "Error";

    public string ObjectType { get; set; }
    public Criteria Criteria { get; set; }
    public string OrderBy { get; set; }
    public bool ReverseOrder { get; set; }

    /// <summary>
    /// Limit number of matches 
    /// </summary>
    public int MaxMatches { get; set; }

    public Guid? FireEventId { get; set; }
}

/// <summary>
/// Update Objects that match criteria
/// NOT IMPLEMENTED YET
/// </summary>
public class BatchUpdateActionOptions : ActionOptions, IActionOptionsForRunner
{
    public const string ObjectNotFoundEventName = "NoObjectsFound";
    public const string ObjectsUpdatedEventName = "ObjectsUpdated";
    public const string ErrorEventName = "Error";

    public string ObjectType { get; set; }
    public Criteria Criteria { get; set; }
    
    /// <summary>
    /// Limit number of results to update 
    /// </summary>
    public int MaxMatches { get; set; }
    
    /// <summary>
    /// Update Operations
    /// </summary>
    public Dictionary<string, object> Updates { get; set; }
    
    /// <summary>
    /// Whether to apply changes straight to the database (build update query)
    /// - it will skip checks, enforcing constraints, ...
    /// - it will not calculate any fields
    /// </summary>
    public bool UnsafeMode { get; set; } 
}