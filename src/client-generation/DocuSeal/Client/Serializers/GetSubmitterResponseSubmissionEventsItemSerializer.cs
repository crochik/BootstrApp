using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmitterResponseSubmissionEventsItem - handles serialization and deserialization
/// </summary>
public static class GetSubmitterResponseSubmissionEventsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmitterResponseSubmissionEventsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmitterResponseSubmissionEventsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmitterResponseSubmissionEventsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmitterResponseSubmissionEventsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmitterResponseSubmissionEventsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        int submitterId = default!;
        string eventType = default!;
        string eventTimestamp = default!;
        object? data = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "submitter_id":
                    submitterId = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "event_type":
                    eventType = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "event_timestamp":
                    eventTimestamp = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "data":
                    data = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmitterResponseSubmissionEventsItem
        {
            Id = id!,
            SubmitterId = submitterId!,
            EventType = eventType!,
            EventTimestamp = eventTimestamp!,
            Data = data
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmitterResponseSubmissionEventsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmitterResponseSubmissionEventsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmitterResponseSubmissionEventsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmitterResponseSubmissionEventsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmitterResponseSubmissionEventsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmitterResponseSubmissionEventsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmitterResponseSubmissionEventsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmitterResponseSubmissionEventsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
