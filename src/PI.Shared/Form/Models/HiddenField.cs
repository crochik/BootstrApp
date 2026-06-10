using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Form.Models;

/// <summary>
/// TODO: it should really be called "system field"
/// ... 
/// </summary>
public class HiddenField : FormField
{
    // TODO: add options?
    // ...
    
    public override bool IsVisible => false;
    
    [JsonIgnore]
    [BsonIgnore]
    public HiddenFieldOptions HiddenFieldOptions
    {
        get => Options as HiddenFieldOptions;
        set => Options = value;
    }    
}

public class HiddenFieldOptions : FieldOptions
{
}
