using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for UpdateSubmitterResponse - handles serialization and deserialization
/// </summary>
public static class UpdateSubmitterResponseSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a UpdateSubmitterResponse instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateSubmitterResponse? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a UpdateSubmitterResponse instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateSubmitterResponse? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static UpdateSubmitterResponse DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        int submissionId = default!;
        string uuid = default!;
        string email = default!;
        string slug = default!;
        string sentAt = default!;
        string openedAt = default!;
        string completedAt = default!;
        string declinedAt = default!;
        string createdAt = default!;
        string updatedAt = default!;
        string name = default!;
        string phone = default!;
        string status = default!;
        string externalId = default!;
        object metadata = default!;
        object preferences = default!;
        List<UpdateSubmitterResponseValuesItem>? values = null;
        List<UpdateSubmitterResponseDocumentsItem>? documents = null;
        string role = default!;
        string embedSrc = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "submission_id":
                    submissionId = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "uuid":
                    uuid = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "email":
                    email = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "slug":
                    slug = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "sent_at":
                    sentAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "opened_at":
                    openedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "completed_at":
                    completedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "declined_at":
                    declinedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "created_at":
                    createdAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "updated_at":
                    updatedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "phone":
                    phone = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "status":
                    status = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "external_id":
                    externalId = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "metadata":
                    metadata = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "preferences":
                    preferences = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "values":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        values = new List<UpdateSubmitterResponseValuesItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = UpdateSubmitterResponseValuesItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                values.Add(_itemValue);
                        }
                    }
                    break;
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<UpdateSubmitterResponseDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = UpdateSubmitterResponseDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
                case "role":
                    role = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "embed_src":
                    embedSrc = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new UpdateSubmitterResponse
        {
            Id = id!,
            SubmissionId = submissionId!,
            Uuid = uuid!,
            Email = email!,
            Slug = slug!,
            SentAt = sentAt!,
            OpenedAt = openedAt!,
            CompletedAt = completedAt!,
            DeclinedAt = declinedAt!,
            CreatedAt = createdAt!,
            UpdatedAt = updatedAt!,
            Name = name!,
            Phone = phone!,
            Status = status!,
            ExternalId = externalId!,
            Metadata = metadata!,
            Preferences = preferences!,
            Values = values!,
            Documents = documents!,
            Role = role!,
            EmbedSrc = embedSrc!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of UpdateSubmitterResponse instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<UpdateSubmitterResponse> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<UpdateSubmitterResponse>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<UpdateSubmitterResponse>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a UpdateSubmitterResponse instance to a JSON string
    /// </summary>
    public static string SerializeToJson(UpdateSubmitterResponse instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of UpdateSubmitterResponse instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<UpdateSubmitterResponse> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
