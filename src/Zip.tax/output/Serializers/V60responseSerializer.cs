using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for V60Response - handles serialization and deserialization
/// </summary>
public static class V60ResponseSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a V60Response instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static V60Response? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a V60Response instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static V60Response? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static V60Response DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        MetadataV60 metadata = default!;
        List<BaseRateV60>? baseRates = null;
        ServiceV60 service = default!;
        ShippingV60 shipping = default!;
        OriginDestinationV60 sourcingRules = default!;
        List<TaxSummaryV60>? taxSummaries = null;
        ProductDetailV60? productDetail = default;
        AddressDetailV60 addressDetail = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "metadata":
                    metadata = MetadataV60Serializer.DeserializeFromElementCore(property.Value);
                    break;
                case "baseRates":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        baseRates = new List<BaseRateV60>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = BaseRateV60Serializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                baseRates.Add(_itemValue);
                        }
                    }
                    break;
                case "service":
                    service = ServiceV60Serializer.DeserializeFromElementCore(property.Value);
                    break;
                case "shipping":
                    shipping = ShippingV60Serializer.DeserializeFromElementCore(property.Value);
                    break;
                case "sourcingRules":
                    sourcingRules = OriginDestinationV60Serializer.DeserializeFromElementCore(property.Value);
                    break;
                case "taxSummaries":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        taxSummaries = new List<TaxSummaryV60>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = TaxSummaryV60Serializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                taxSummaries.Add(_itemValue);
                        }
                    }
                    break;
                case "productDetail":
                    productDetail = ProductDetailV60Serializer.DeserializeFromElementCore(property.Value);
                    break;
                case "addressDetail":
                    addressDetail = AddressDetailV60Serializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new V60Response
        {
            Metadata = metadata!,
            BaseRates = baseRates!,
            Service = service!,
            Shipping = shipping!,
            SourcingRules = sourcingRules!,
            TaxSummaries = taxSummaries!,
            ProductDetail = productDetail,
            AddressDetail = addressDetail!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of V60Response instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<V60Response> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<V60Response>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<V60Response>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a V60Response instance to a JSON string
    /// </summary>
    public static string SerializeToJson(V60Response instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of V60Response instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<V60Response> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
