using System;
using MongoDB.Bson;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace PI.Shared.Json;

public class ObjectIdConverter : JsonConverter<ObjectId>
{
    public override void WriteJson(JsonWriter writer, ObjectId value, JsonSerializer serializer)
    {
        // serialize as a UUID string
        writer.WriteValue(value.ToGuid().ToString());
    }

    public override ObjectId ReadJson(JsonReader reader, Type objectType, ObjectId existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);
        if (token.Type == JTokenType.String)
        {
            var str = token.ToString();
            if (str.Length == 24 && ObjectId.TryParse(str, out var objectId)) return objectId;
            if (Guid.TryParse(str, out var uuid) && uuid.TryGetObjectId(out objectId))
            {
                return objectId;
            }
        }

        // Handle other cases or throw an error
        throw new JsonSerializationException($"Unexpected token type {token.Type} when deserializing ObjectId.");
    }
}

public class Decimal128Converter : JsonConverter<Decimal128>
{
    public override void WriteJson(JsonWriter writer, Decimal128 value, JsonSerializer serializer)
    {
        // Convert Decimal128 to .NET decimal and write as a number
        writer.WriteValue((decimal)value);
    }

    public override Decimal128 ReadJson(JsonReader reader, Type objectType, Decimal128 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);

        if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
        {
            // Read as a decimal and convert to Decimal128
            return (Decimal128)token.ToObject<decimal>();
        }

        if (token.Type == JTokenType.String)
        {
            // If it's a string, try parsing it as a decimal and convert
            if (decimal.TryParse(token.ToString(), out decimal decimalValue))
            {
                return (Decimal128)decimalValue;
            }
        }

        // Handle other cases or throw an error
        throw new JsonSerializationException($"Unexpected token type {token.Type} when deserializing Decimal128.");
    }
}

public class FlagsEnumConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        var underlyingType = Nullable.GetUnderlyingType(objectType) ?? objectType;
        return underlyingType.IsEnum && underlyingType.GetCustomAttributes(typeof(FlagsAttribute), true).Length > 0;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteValue(0);
        }
        else
        {
            writer.WriteValue((int)value);
        }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        return Enum.ToObject(objectType, reader.ReadAsInt32().GetValueOrDefault());
    }
}

public class DefaultEnumJsonConverter : StringEnumConverter
{
    public DefaultEnumJsonConverter()
    {
        NamingStrategy = new DefaultNamingStrategy();
        AllowIntegerValues = false;
    }

    public override bool CanConvert(Type objectType)
    {
        var underlyingType = Nullable.GetUnderlyingType(objectType) ?? objectType;
        return underlyingType.IsEnum && underlyingType.GetCustomAttributes(typeof(FlagsAttribute), true).Length == 0; // Check it is not Flags enum
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        Enum e = (Enum)value;

        var str = e.ToString();
        writer.WriteValue(str);
    }
}