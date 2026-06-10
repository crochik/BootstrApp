using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionsFromEmailsRequest - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionsFromEmailsRequestSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionsFromEmailsRequest instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionsFromEmailsRequest? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionsFromEmailsRequest instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionsFromEmailsRequest? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionsFromEmailsRequest DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int templateId = default!;
        string emails = default!;
        bool? sendEmail = default;
        CreateSubmissionsFromEmailsRequestMessage? message = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "template_id":
                    templateId = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "emails":
                    emails = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "send_email":
                    sendEmail = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "message":
                    message = CreateSubmissionsFromEmailsRequestMessageSerializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateSubmissionsFromEmailsRequest
        {
            TemplateId = templateId!,
            Emails = emails!,
            SendEmail = sendEmail,
            Message = message
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionsFromEmailsRequest instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionsFromEmailsRequest> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionsFromEmailsRequest>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionsFromEmailsRequest>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionsFromEmailsRequest instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionsFromEmailsRequest instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionsFromEmailsRequest instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionsFromEmailsRequest> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
