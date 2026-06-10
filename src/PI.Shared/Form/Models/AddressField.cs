using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class AddressField : FormField
{
    public override BackingType GetBackingType() => BackingType.String;
    
    [JsonIgnore]
    [BsonIgnore]
    public AddressFieldOptions AddressFieldOptions
    {
        get => Options as AddressFieldOptions;
        set => Options = value;
    }    
}

public class AddressFieldOptions : FieldOptions
{
}