using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class EmailField : FormField
{
    public override BackingType GetBackingType() => BackingType.String;

    public override object AutoConvert(object value)
    {
        value = base.AutoConvert(value);

        return value is string str ? str.ToLowerInvariant() : value;
    }
    
    [JsonIgnore]
    [BsonIgnore]
    public EmailFieldOptions EmailFieldOptions
    {
        get => Options as EmailFieldOptions;
        set => Options = value;
    }    
}

public class EmailFieldOptions : FieldOptions
{
}