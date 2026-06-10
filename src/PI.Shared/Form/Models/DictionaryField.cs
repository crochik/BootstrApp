using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PI.Shared.Extensions;

namespace PI.Shared.Form.Models;

/// <summary>
/// For now limited to a (string) key and a "simple" (scalar) value
/// Use ChildrenObject for "complex" (e.g. object) values
/// </summary>
public class DictionaryField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public DictionaryFieldOptions DictionaryFieldOptions
    {
        get => Options as DictionaryFieldOptions;
        set => Options = value;
    }

    public DictionaryField()
    {
        DictionaryFieldOptions = new DictionaryFieldOptions();
    }

    public override object AutoConvert(object value)
    {
        if (value is JObject jObject)
        {
            value = jObject.Properties().ToDictionary();
        }

        // TODO: probably should defer to the fields in the options to convert the values of each individual kvp 
        // ... 

        var converted = base.AutoConvert(value);
        return converted;
    }
}