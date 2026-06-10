using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class TagsField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public TagsFieldOptions TagFieldOptions
    {
        get => Options as TagsFieldOptions;
        set => Options = value;
    }

    public TagsField()
    {
        TagFieldOptions = new TagsFieldOptions();
    }

    public override BackingType GetBackingType() => BackingType.StringArray;
}