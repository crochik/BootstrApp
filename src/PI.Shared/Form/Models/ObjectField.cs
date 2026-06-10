using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PI.Shared.Extensions;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

/// <summary>
/// allow embedding object as a property
/// </summary>
public class ObjectField : FormField, IComplexFieldValue
{
    [JsonIgnore]
    [BsonIgnore]
    public ObjectFieldOptions ObjectFieldOptions
    {
        get => Options as ObjectFieldOptions;
        set => Options = value;
    }

    public override BackingType GetBackingType() => new ObjectBackingType
    {
        IsArray = false,
        IsDictionary = false,
        ObjectType = ObjectFieldOptions?.ObjectType,
    };

    public override object AutoConvert(object value)
    {
        return value switch
        {
            JObject jObject => jObject.Properties().ToDictionary(),
            _ => base.AutoConvert(value)
        };
    }
}