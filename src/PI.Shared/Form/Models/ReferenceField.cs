using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using ValueType = PI.Shared.Models.ValueType;

namespace PI.Shared.Form.Models;

/// <summary>
/// Reference to other object
/// the value is generally the _id, name or externalId of the other object
/// </summary>
public class ReferenceField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public ReferenceFieldOptions ReferenceFieldOptions
    {
        get => Options as ReferenceFieldOptions;
        set => Options = value;
    }

    public ReferenceField()
    {
        ReferenceFieldOptions = new ReferenceFieldOptions();
    }

    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        base.FillPlaceHolders(context, objectContext);

        // TODO: maybe other fields
        // ReferenceFieldOptions.ObjectType
        // ReferenceFieldOptions.ForeignFieldName
        // ...

        ReferenceFieldOptions?.FillPlaceHolders(context, objectContext);
    }

    /// <summary>
    /// Get value type
    /// Assumes String if not defined
    /// </summary>
    public override BackingType GetBackingType()
    {
        return ReferenceFieldOptions?.ValueType != null ? new BackingType { ValueType = ReferenceFieldOptions.ValueType.Value } : BackingType.String;
    }
    
    /// <summary>
    /// "Infer" object type based on the criteria  
    /// </summary>
    public bool TryGetObjectTypeFromCriteria(out string objectType, string currentObjectType = null)
    {
        if (ReferenceFieldOptions?.Criteria == null)
        {
            objectType = null;
            return false;
        }

        var condition = ReferenceFieldOptions.Criteria.FirstOrDefault(x => x.FieldName == nameof(IFlowObject.ObjectType));
        objectType = condition?.Value switch
        {
            null => null,
            "{{ObjectType}}" => currentObjectType,
            string str => str.Contains("{{") ? null : str,
            _ => null,
        };

        return !string.IsNullOrEmpty(objectType);
    }
}

/// <summary>
/// Reference field that allows to select multiple options
/// add "All" and "None" to the items as necessary
/// </summary>
public class MultiReferenceField : FormField
{
    public override string Type => "multiReference";

    [JsonIgnore]
    [BsonIgnore]
    public MultiReferenceFieldOptions MultiReferenceFieldOptions
    {
        get => Options as MultiReferenceFieldOptions;
        set => Options = value;
    }

    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        base.FillPlaceHolders(context, objectContext);

        MultiReferenceFieldOptions?.Criteria.ReplaceValuePlaceHolders(objectContext);

        // TODO: maybe other fields
        // ReferenceFieldOptions.ObjectType
        // ReferenceFieldOptions.ForeignFieldName
        // ...
    }
    
    /// <summary>
    /// Get value type
    /// Assumes String if not defined
    /// </summary>
    public override BackingType GetBackingType()
    {
        return new BackingType
        {
            IsArray = true,
            ValueType = MultiReferenceFieldOptions?.ValueType ?? ValueType.String,
        };
    }
}