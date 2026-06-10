using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

/// <summary>
/// Property is a collection of Objects
/// </summary>
public class ChildrenField : FormField, IComplexFieldValue
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new DefaultContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    [JsonIgnore]
    [BsonIgnore]
    public ChildrenFieldOptions ChildrenFieldOptions
    {
        get => Options as ChildrenFieldOptions;
        set => Options = value;
    }

    public override BackingType GetBackingType() => new ObjectBackingType
    {
        IsArray = ChildrenFieldOptions?.KeyType == ChildrenFieldOptions.IndexKeyType,
        IsDictionary = ChildrenFieldOptions?.KeyType == ChildrenFieldOptions.StringKeyType,
        ObjectType = ChildrenFieldOptions?.ObjectType,
    };

    public override object AutoConvert(object value)
    {
        if (ChildrenFieldOptions?.KeyType == ChildrenFieldOptions.IndexKeyType && value is JArray jArray)
        {
            // special handling for how the front end sends an array
            value = jArray
                .Children()
                .Select(JsonConvert.SerializeObject)
                .Select(x => JsonConvert.DeserializeObject<ExpandoObject>(x, JsonSerializerSettings))
                .ToArray();
        }

        return base.AutoConvert(value);
    }
}