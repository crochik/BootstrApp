using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Form.Models;

public class FileField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public FileFieldOptions FileFieldOptions
    {
        get => Options as FileFieldOptions;
        set => Options = value;
    }
}