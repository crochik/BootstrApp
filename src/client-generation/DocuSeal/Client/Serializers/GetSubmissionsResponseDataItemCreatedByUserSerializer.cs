using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmissionsResponseDataItemCreatedByUser - handles serialization and deserialization
/// </summary>
public static class GetSubmissionsResponseDataItemCreatedByUserSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmissionsResponseDataItemCreatedByUser instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionsResponseDataItemCreatedByUser? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmissionsResponseDataItemCreatedByUser instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionsResponseDataItemCreatedByUser? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmissionsResponseDataItemCreatedByUser DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        string firstName = default!;
        string lastName = default!;
        string email = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "first_name":
                    firstName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "last_name":
                    lastName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "email":
                    email = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmissionsResponseDataItemCreatedByUser
        {
            Id = id!,
            FirstName = firstName!,
            LastName = lastName!,
            Email = email!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmissionsResponseDataItemCreatedByUser instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmissionsResponseDataItemCreatedByUser> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmissionsResponseDataItemCreatedByUser>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmissionsResponseDataItemCreatedByUser>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmissionsResponseDataItemCreatedByUser instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmissionsResponseDataItemCreatedByUser instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmissionsResponseDataItemCreatedByUser instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmissionsResponseDataItemCreatedByUser> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
