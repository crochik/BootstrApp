using System;
using System.Collections.Generic;
using System.Linq;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace Messages.Flow;

/// <summary>
/// Used for LMS to increment counts and track cost
/// </summary>
public class LeadTypeServiceUsageActionOptions : ActionOptions, IActionOptionsForRunner
{
    public const string TAG_OVER_BUDGET = "Over Budget";
    public const string TAG_OVER_BUDGET_SOURCE = "Over Budget: Source";
    public const string TAG_OVER_BUDGET_ORG= "Over Budget: Organization";
    public const string TAG_OVER_BUDGET_SERVICE= "Over Budget: Service";
    public const string TAG_OVER_BUDGET_POSTALCODE= "Over Budget: Postal Code";
    
    public const string OnBudgetEvent = nameof(OnBudgetEvent);
    public const string OverBudgetEvent = nameof(OverBudgetEvent);
    
    /// <summary>
    /// Path to the value that should be used to track service 
    /// </summary>
    public string Service { get; set; }
    
    // public ObjectType BuildActionOptionsObjectType(IEntityContext context)
    // {
    //     var objectType = new ObjectType
    //     {
    //         Id = Guid.NewGuid(),
    //         Name = $"{nameof(ActionIds.LeadTypeServiceUsage)}ActionOptions",
    //         Description = "Lead Type Service Budget Options",
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
    //         yield return new TextField
    //         {
    //             Name = nameof(Service),
    //             Label = "Service (expression)",
    //             Description = "Service to check usage. Value can be an Expression.",
    //             IsRequired = true,
    //             TextFieldOptions = new TextFieldOptions
    //             {
    //                 AllowExpressions = true,
    //             },
    //             DefaultValue = "{{Object.ParsedInput.Service}}"
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
    //         Name = nameof(ActionIds.LeadTypeServiceUsage),
    //         Description = "Update Object",
    //         ActionId = ActionIds.LeadTypeServiceUsage,
    //         InputObjectTypes = new [] { "LMSTransaction" },
    //         IconName = null,
    //         ActionOptionsObjectType = $"{nameof(ActionIds.LeadTypeServiceUsage)}ActionOptions",
    //         Role = EntityRoleId.Admin,
    //         ProfileIds = null,
    //         Outputs = new Dictionary<string, string>
    //         {
    //             { OnBudgetEvent, "Service active" },
    //             { OverBudgetEvent, "Service over budget or disabled" }
    //         }
    //     };
    //
    //     return action;
    // }
}