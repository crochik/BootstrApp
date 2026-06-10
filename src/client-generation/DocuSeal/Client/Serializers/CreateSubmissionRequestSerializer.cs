using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionRequest - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionRequestSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionRequest instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionRequest? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionRequest instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionRequest? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionRequest DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int templateId = default!;
        bool? sendEmail = default;
        bool? sendSms = default;
        string? order = default;
        string? completedRedirectUrl = default;
        string? bccCompleted = default;
        string? replyTo = default;
        string? expireAt = default;
        CreateSubmissionRequestMessage? message = default;
        List<CreateSubmissionRequestSubmittersItem>? submitters = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "template_id":
                    templateId = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "send_email":
                    sendEmail = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "send_sms":
                    sendSms = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "order":
                    order = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "completed_redirect_url":
                    completedRedirectUrl = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "bcc_completed":
                    bccCompleted = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "reply_to":
                    replyTo = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "expire_at":
                    expireAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "message":
                    message = CreateSubmissionRequestMessageSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "submitters":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        submitters = new List<CreateSubmissionRequestSubmittersItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateSubmissionRequestSubmittersItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                submitters.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateSubmissionRequest
        {
            TemplateId = templateId!,
            SendEmail = sendEmail,
            SendSms = sendSms,
            Order = order,
            CompletedRedirectUrl = completedRedirectUrl,
            BccCompleted = bccCompleted,
            ReplyTo = replyTo,
            ExpireAt = expireAt,
            Message = message,
            Submitters = submitters!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionRequest instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionRequest> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionRequest>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionRequest>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionRequest instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionRequest instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionRequest instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionRequest> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
