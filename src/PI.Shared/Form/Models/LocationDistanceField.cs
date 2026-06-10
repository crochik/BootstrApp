using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

/// <summary>
/// Field used to filter locations (by distance)
/// </summary>
public class LocationDistanceField : FormField, IComplexFieldValue, IDynamicFieldValue
{
    public override BackingType GetBackingType() => BackingType.Decimal;

    [JsonIgnore]
    [BsonIgnore]
    public LocationDistanceFieldOptions LocationDistanceFieldOptions
    {
        get => Options as LocationDistanceFieldOptions;
        set => Options = value;
    }
    
    public override IEnumerable<string> GetDependencies(bool forCalculation = false, bool requiredOutput = false)
    {
        if (forCalculation)
        {
            if (!string.IsNullOrWhiteSpace(LocationDistanceFieldOptions?.LocationFieldName)) yield return LocationDistanceFieldOptions?.LocationFieldName;
        }
    }
}

public class LocationDistanceFieldOptions : FieldOptions
{
    public string LocationFieldName { get; set; }
}
