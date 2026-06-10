using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Crochik.Dipper;

public class Parameter
{
    public string Name { get; set; }
    public object DefaultValue { get; set; }
    public bool IsRequired { get; set; } = true;

    [BsonRepresentation(BsonType.String)]
    [JsonConverter(typeof(StringEnumConverter))]
    public BsonType Type { get; set; } = BsonType.String;
}