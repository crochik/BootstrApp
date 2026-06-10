using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Form.Models;

/// <summary>
/// Related objects (a way to embed related objects in the same form... probably should be deprecated... )
/// </summary>
[Obsolete("not really a field")]
public class RelatedObjectsField : FormField, IComplexFieldValue
{
    /// <summary>
    /// temporary until the FE is updated
    /// </summary>
    public override string Type => "relatedObjects";

    [JsonIgnore]
    [BsonIgnore]
    public RelatedObjectsFieldOptions RelatedObjectsOptions
    {
        get => Options as RelatedObjectsFieldOptions;
        set => Options = value;
    }
}