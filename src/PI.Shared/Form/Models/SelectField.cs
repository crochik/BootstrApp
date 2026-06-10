using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class SelectField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public SelectFieldOptions SelectFieldOptions
    {
        get => Options as SelectFieldOptions;
        set => Options = value;
    }
}

public class MultiSelectField : FormField
{
    public override string Type => "multiSelect";
    
    [JsonIgnore]
    [BsonIgnore]
    public MultiSelectFieldOptions MultiSelectFieldOptions
    {
        get => Options as MultiSelectFieldOptions;
        set => Options = value;
    }
}

public class BitwiseFlagField : FormField
{
    public override object AutoConvert(object value)
    {
        return base.AutoConvert(value);
    }

    public override BackingType GetBackingType() => BackingType.Int64;

    [JsonIgnore]
    [BsonIgnore]
    public BitwiseFlagFieldOptions BitwiseFlagFieldOptions 
    {
        get => Options as BitwiseFlagFieldOptions;
        set => Options = value;
    }
}