using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmissionResponseSubmissionEventsItem - handles serialization and deserialization
/// </summary>
public static class GetSubmissionResponseSubmissionEventsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmissionResponseSubmissionEventsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionResponseSubmissionEventsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmissionResponseSubmissionEventsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionResponseSubmissionEventsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmissionResponseSubmissionEventsItem DeserializeFromElementCore(JsonElement element)
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
        return new GetSubmissionResponseSubmissionEventsItem
        {
            Id = id!,
            SubmitterId = submitterId!,
            EventType = eventType!,
            EventTimestamp = eventTimestamp!,
            Data = data
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmissionResponseSubmissionEventsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmissionResponseSubmissionEventsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmissionResponseSubmissionEventsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmissionResponseSubmissionEventsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmissionResponseSubmissionEventsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmissionResponseSubmissionEventsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmissionResponseSubmissionEventsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmissionResponseSubmissionEventsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
