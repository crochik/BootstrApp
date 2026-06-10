using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class UrlField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public URLFieldOptions URLFieldOptions
    {
        get => Options as URLFieldOptions;
        set => Options = value;
    }

    public UrlField()
    {
        URLFieldOptions = new URLFieldOptions();
    }

    public override BackingType GetBackingType() => BackingType.String;
}