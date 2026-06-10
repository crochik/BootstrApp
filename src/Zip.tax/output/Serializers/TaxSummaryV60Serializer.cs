using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for TaxSummaryV60 - handles serialization and deserialization
/// </summary>
public static class TaxSummaryV60Serializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a TaxSummaryV60 instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static TaxSummaryV60? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a TaxSummaryV60 instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static TaxSummaryV60? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static TaxSummaryV60 DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        float rate = default!;
        TaxSummaryV60TaxType taxType = default!;
        string summaryName = default!;
        List<DisplayRate>? displayRates = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "rate":
                    rate = BaseSerializer.ParsePrimitivefloat(property.Value);
                    break;
                case "taxType":
                    var taxTypeValue = TaxSummaryV60TaxTypeSerializer.DeserializeFromElement(property.Value);
                    if (taxTypeValue != null)
                        taxType = taxTypeValue.Value;
                    break;
                case "summaryName":
                    summaryName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "displayRates":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        displayRates = new List<DisplayRate>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = DisplayRateSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                displayRates.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new TaxSummaryV60
        {
            Rate = rate!,
            TaxType = taxType!,
            SummaryName = summaryName!,
            DisplayRates = displayRates!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of TaxSummaryV60 instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<TaxSummaryV60> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<TaxSummaryV60>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<TaxSummaryV60>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a TaxSummaryV60 instance to a JSON string
    /// </summary>
    public static string SerializeToJson(TaxSummaryV60 instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of TaxSummaryV60 instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<TaxSummaryV60> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
