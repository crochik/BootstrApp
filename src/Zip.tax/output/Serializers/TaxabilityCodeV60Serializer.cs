using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for TaxabilityCodeV60 - handles serialization and deserialization
/// </summary>
public static class TaxabilityCodeV60Serializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a TaxabilityCodeV60 instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static TaxabilityCodeV60? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a TaxabilityCodeV60 instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static TaxabilityCodeV60? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static TaxabilityCodeV60 DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string id = default!;
        string stateFips = default!;
        string countyFips = default!;
        string title = default!;
        string label = default!;
        string rateActionCode = default!;
        string rateActionMessage = default!;
        List<RateRuleV60>? rateRules = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "stateFIPS":
                    stateFips = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "countyFIPS":
                    countyFips = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "title":
                    title = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "label":
                    label = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "rateActionCode":
                    rateActionCode = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "rateActionMessage":
                    rateActionMessage = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "rateRules":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        rateRules = new List<RateRuleV60>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = RateRuleV60Serializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                rateRules.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new TaxabilityCodeV60
        {
            Id = id!,
            StateFips = stateFips!,
            CountyFips = countyFips!,
            Title = title!,
            Label = label!,
            RateActionCode = rateActionCode!,
            RateActionMessage = rateActionMessage!,
            RateRules = rateRules!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of TaxabilityCodeV60 instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<TaxabilityCodeV60> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<TaxabilityCodeV60>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<TaxabilityCodeV60>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a TaxabilityCodeV60 instance to a JSON string
    /// </summary>
    public static string SerializeToJson(TaxabilityCodeV60 instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of TaxabilityCodeV60 instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<TaxabilityCodeV60> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
