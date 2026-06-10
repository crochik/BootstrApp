using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for AddressComponentsV60 - handles serialization and deserialization
/// </summary>
public static class AddressComponentsV60Serializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a AddressComponentsV60 instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static AddressComponentsV60? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a AddressComponentsV60 instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static AddressComponentsV60? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static AddressComponentsV60 DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string countryCode = default!;
        string countryName = default!;
        string stateCode = default!;
        string state = default!;
        string county = default!;
        string city = default!;
        string street = default!;
        string postalCode = default!;
        string houseNumber = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "countryCode":
                    countryCode = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "countryName":
                    countryName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "stateCode":
                    stateCode = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "state":
                    state = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "county":
                    county = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "city":
                    city = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "street":
                    street = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "postalCode":
                    postalCode = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "houseNumber":
                    houseNumber = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new AddressComponentsV60
        {
            CountryCode = countryCode!,
            CountryName = countryName!,
            StateCode = stateCode!,
            State = state!,
            County = county!,
            City = city!,
            Street = street!,
            PostalCode = postalCode!,
            HouseNumber = houseNumber!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of AddressComponentsV60 instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<AddressComponentsV60> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<AddressComponentsV60>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<AddressComponentsV60>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a AddressComponentsV60 instance to a JSON string
    /// </summary>
    public static string SerializeToJson(AddressComponentsV60 instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of AddressComponentsV60 instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<AddressComponentsV60> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
