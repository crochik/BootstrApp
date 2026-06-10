using System;
using System.Collections.Generic;
using MongoDB.Bson;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Swashbuckle.AspNetCore.Annotations;
using ValueType = PI.Shared.Models.ValueType;

namespace PI.Shared.Form.Models;

public enum JoinBehavior
{
    /// <summary>
    /// Filter lookup results but does not exclude (when visible)
    /// e.g. LEFT JOIN
    /// </summary>
    FilterOnly,

    /// <summary>
    /// lookup without constraints/criteria (when visible)
    /// only uses localField/ExternalId
    /// </summary>
    Unsafe,

    /// <summary>
    /// Apply criteria and filter even when not visible
    /// e.g. INNER JOIN
    /// </summary>
    Exclude,
}

public enum ContributeUserEvents
{
    Never,

    /// <summary>
    /// only when the parent object is being showed individually (e.g. Form) 
    /// </summary>
    Form,

    /// <summary>
    /// only when in a dataView
    /// </summary>
    View,

    Always,
}

[SwaggerSubType(typeof(MultiReferenceFieldOptions), DiscriminatorValue = nameof(MultiReferenceFieldOptions))]
[SwaggerSubType(typeof(RemoteFileFieldOptions), DiscriminatorValue = nameof(RemoteFileFieldOptions))]
[SwaggerSubType(typeof(AppointmentFieldOptions), DiscriminatorValue = nameof(AppointmentFieldOptions))]
[SwaggerSubType(typeof(ChatReferenceFieldOptions), DiscriminatorValue = nameof(ChatReferenceFieldOptions))]
public class ReferenceFieldOptions : SelectFieldOptions
{
    /// <summary>
    /// TODO: rename to Url for consistency  
    /// </summary>
    public string ObjectType { get; set; }

    /// <summary>
    /// Criteria to limit existing items that can be selected
    /// the Eq conditions will also be used to initialize any form in actions
    /// </summary>
    public Condition[] Criteria { get; set; }

    /// <summary>
    /// Whether the user can search for an existing item
    /// (only when there are actions?)
    /// </summary>
    public bool AutoComplete { get; set; } = true;

    /// <summary>
    /// field name to be used as the key in the objectType
    /// </summary>
    public string ForeignFieldName { get; set; }

    // TODO: use a form to represent a card?
    // /// <summary>
    // /// Whether to show description
    // /// </summary>
    // public bool ShowDescription { get; set; }
    //     
    // /// <summary>
    // /// Whether to show thumbnail
    // /// </summary>
    // public bool ShowImage { get; set; }

    public ValueType? ValueType { get; set; }
    
    /// <summary>
    /// Actions that can be taken on related object
    /// use Enabled,Visible to control whether can done if the value is set or not ?
    /// </summary>
    public FormAction[] Actions { get; set; }
    
    public JoinBehavior? JoinBehavior { get; set; }
    
    public ContributeUserEvents? ContributeUserEvents { get; set; }

    public virtual void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        Criteria.ReplaceValuePlaceHolders(objectContext);
    }
}

/// <summary>
/// Reference to a AI Gen Chat, will display last generated contennt  
/// </summary>
public class ChatReferenceFieldOptions : ReferenceFieldOptions
{
    /// <summary>
    /// Assistant to be used to create content
    /// </summary>
    public Guid AssistantId { get; set; }
    
    /// <summary>
    /// Context for flow (expression)
    /// </summary>
    public string ObjectTypeToReference { get; set; }
    
    /// <summary>
    /// Context for flow (expression)
    /// </summary>
    public string ObjectIdToReference { get; set; }

    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        base.FillPlaceHolders(context, objectContext);

        if (ExpressionEvaluatorService.TryResolve(context, objectContext, ObjectTypeToReference, out var objectType))
        {
            ObjectTypeToReference = objectType?.ToString();
        }
        
        if (ExpressionEvaluatorService.TryResolve(context, objectContext, ObjectIdToReference, out var objectId))
        {
            ObjectIdToReference = objectId switch
            {
                Guid guid => guid.ToString(),
                ObjectId oid => oid.ToGuid().ToString(),
                _ => objectId?.ToString(),
            };
        }
    }
}

/// <summary>
/// Reference field that allows to select multiple options
/// add "All" and "None" to the items as necessary
/// </summary>
public class MultiReferenceFieldOptions : ReferenceFieldOptions
{
}