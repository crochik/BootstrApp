using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class ArrayField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public ArrayFieldOptions ArrayFieldOptions
    {
        get => Options as ArrayFieldOptions;
        set => Options = value;
    }

    public override BackingType GetBackingType()
    {
        var singleBacking = ArrayFieldOptions?.ValueField.GetBackingType();
        return new BackingType
        {
            IsArray = true,
            ValueType = singleBacking?.ValueType ?? ValueType.Unknown
        };
    }
}