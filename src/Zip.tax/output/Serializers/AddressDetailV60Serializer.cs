using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for AddressDetailV60 - handles serialization and deserialization
/// </summary>
public static class AddressDetailV60Serializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a AddressDetailV60 instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static AddressDetailV60? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a AddressDetailV60 instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static AddressDetailV60? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static AddressDetailV60 DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string normalizedAddress = default!;
        AddressDetailV60Incorporated incorporated = default!;
        float geoLat = default!;
        float geoLng = default!;
        AddressComponentsV60? address = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "normalizedAddress":
                    normalizedAddress = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "incorporated":
                    var incorporatedValue = AddressDetailV60IncorporatedSerializer.DeserializeFromElement(property.Value);
                    if (incorporatedValue != null)
                        incorporated = incorporatedValue.Value;
                    break;
                case "geoLat":
                    geoLat = BaseSerializer.ParsePrimitivefloat(property.Value);
                    break;
                case "geoLng":
                    geoLng = BaseSerializer.ParsePrimitivefloat(property.Value);
                    break;
                case "address":
                    address = AddressComponentsV60Serializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new AddressDetailV60
        {
            NormalizedAddress = normalizedAddress!,
            Incorporated = incorporated!,
            GeoLat = geoLat!,
            GeoLng = geoLng!,
            Address = address
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of AddressDetailV60 instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<AddressDetailV60> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<AddressDetailV60>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<AddressDetailV60>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a AddressDetailV60 instance to a JSON string
    /// </summary>
    public static string SerializeToJson(AddressDetailV60 instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of AddressDetailV60 instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<AddressDetailV60> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
