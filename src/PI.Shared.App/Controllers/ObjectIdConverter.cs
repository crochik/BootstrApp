using System;
using MongoDB.Bson;
using Newtonsoft.Json;

namespace PI.Shared.Converters;

/// <summary>
/// Serialize ObjectIds as Guid strings prefixed by 00000000
/// Currently doesn't handle deserializing into it
/// </summary>
public class ObjectIdConverter : JsonConverter<ObjectId>
{
    public override ObjectId ReadJson(JsonReader reader, Type objectType, ObjectId existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override void WriteJson(JsonWriter writer, ObjectId value, JsonSerializer serializer)
        => writer.WriteValue(value.ToGuid().ToString());
}