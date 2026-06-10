using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api;

/// <summary>
/// Base class for all serializers - provides common serialization helpers and primitive parsers
/// </summary>
public abstract class BaseSerializer
{
    // ==================== Common Serialization Options ====================

    /// <summary>
    /// Gets default serializer options for deserialization
    /// </summary>
    internal static JsonSerializerOptions GetDefaultDeserializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Gets default serializer options for serialization
    /// </summary>
    internal static JsonSerializerOptions GetDefaultSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    // ==================== Primitive Type Parsers ====================

    /// <summary>
    /// Parses a string value from JsonElement
    /// </summary>
    internal static string ParsePrimitivestring(JsonElement element)
    {
        return element.GetString() ?? string.Empty;
    }

    /// <summary>
    /// Parses an int value from JsonElement
    /// </summary>
    internal static int ParsePrimitiveint(JsonElement element)
    {
        return element.GetInt32();
    }

    /// <summary>
    /// Parses a long value from JsonElement
    /// </summary>
    internal static long ParsePrimitivelong(JsonElement element)
    {
        return element.GetInt64();
    }

    /// <summary>
    /// Parses a bool value from JsonElement
    /// </summary>
    internal static bool ParsePrimitivebool(JsonElement element)
    {
        return element.GetBoolean();
    }

    /// <summary>
    /// Parses a double value from JsonElement
    /// </summary>
    internal static double ParsePrimitivedouble(JsonElement element)
    {
        return element.GetDouble();
    }

    /// <summary>
    /// Parses a float value from JsonElement
    /// </summary>
    internal static float ParsePrimitivefloat(JsonElement element)
    {
        return (float)element.GetDouble();
    }

    /// <summary>
    /// Parses a decimal value from JsonElement
    /// </summary>
    internal static decimal ParsePrimitivedecimal(JsonElement element)
    {
        return element.GetDecimal();
    }

    /// <summary>
    /// Parses a DateTime value from JsonElement
    /// </summary>
    internal static DateTime ParsePrimitiveDateTime(JsonElement element)
    {
        return element.GetDateTime();
    }

    /// <summary>
    /// Parses a DateTimeOffset value from JsonElement
    /// </summary>
    internal static DateTimeOffset ParsePrimitiveDateTimeOffset(JsonElement element)
    {
        return element.GetDateTimeOffset();
    }

    /// <summary>
    /// Parses a DateOnly value from JsonElement
    /// </summary>
    internal static DateOnly ParsePrimitiveDateOnly(JsonElement element)
    {
        var str = element.GetString();
        if (string.IsNullOrEmpty(str))
            throw new JsonException("DateOnly value cannot be null or empty");

        return DateOnly.Parse(str);
    }

    /// <summary>
    /// Parses a TimeOnly value from JsonElement
    /// </summary>
    internal static TimeOnly ParsePrimitiveTimeOnly(JsonElement element)
    {
        var str = element.GetString();
        if (string.IsNullOrEmpty(str))
            throw new JsonException("TimeOnly value cannot be null or empty");

        return TimeOnly.Parse(str);
    }

    /// <summary>
    /// Parses a Guid value from JsonElement
    /// </summary>
    internal static Guid ParsePrimitiveGuid(JsonElement element)
    {
        return element.GetGuid();
    }

    /// <summary>
    /// Parses a byte value from JsonElement
    /// </summary>
    internal static byte ParsePrimitivebyte(JsonElement element)
    {
        return element.GetByte();
    }

    /// <summary>
    /// Parses a short value from JsonElement
    /// </summary>
    internal static short ParsePrimitiveshort(JsonElement element)
    {
        return element.GetInt16();
    }

    /// <summary>
    /// Parses a uint value from JsonElement
    /// </summary>
    internal static uint ParsePrimitiveuint(JsonElement element)
    {
        return element.GetUInt32();
    }

    /// <summary>
    /// Parses a ulong value from JsonElement
    /// </summary>
    internal static ulong ParsePrimitiveulong(JsonElement element)
    {
        return element.GetUInt64();
    }

    /// <summary>
    /// Parses a ushort value from JsonElement
    /// </summary>
    internal static ushort ParsePrimitiveushort(JsonElement element)
    {
        return element.GetUInt16();
    }

    /// <summary>
    /// Parses an sbyte value from JsonElement
    /// </summary>
    internal static sbyte ParsePrimitivesbyte(JsonElement element)
    {
        return element.GetSByte();
    }

    /// <summary>
    /// Parses an object value from JsonElement
    /// Returns the raw JsonElement for dynamic/untyped data
    /// </summary>
    internal static object? ParsePrimitiveobject(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return null;

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(element.GetRawText()),
            JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText()),
            _ => element.GetRawText()
        };
    }

    // ==================== Nullable Primitive Type Parsers ====================

    /// <summary>
    /// Safely parses a nullable value from JsonElement
    /// </summary>
    internal static T? ParseNullable<T>(JsonElement element, Func<JsonElement, T> parser) where T : struct
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return null;

        return parser(element);
    }

    // ==================== Validation Helpers ====================

    /// <summary>
    /// Validates that a required property exists in the JSON
    /// </summary>
    internal static void ValidateRequiredProperty(bool exists, string propertyName)
    {
        if (!exists)
        {
            throw new JsonException($"Required property '{propertyName}' not found in JSON");
        }
    }

    /// <summary>
    /// Checks if JsonElement represents a null or undefined value
    /// </summary>
    internal static bool IsNullOrUndefined(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined;
    }
}
