using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionFromHtmlRequest - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionFromHtmlRequestSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionFromHtmlRequest instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromHtmlRequest? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionFromHtmlRequest instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromHtmlRequest? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionFromHtmlRequest DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? name = default;
        bool? sendEmail = default;
        bool? sendSms = default;
        string? order = default;
        string? completedRedirectUrl = default;
        string? bccCompleted = default;
        string? replyTo = default;
        string? expireAt = default;
        List<int>? templateIds = null;
        List<CreateSubmissionFromHtmlRequestDocumentsItem>? documents = null;
        List<CreateSubmissionFromHtmlRequestSubmittersItem>? submitters = null;
        CreateSubmissionFromHtmlRequestMessage? message = default;
        bool? mergeDocuments = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
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
                case "template_ids":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        templateIds = new List<int>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            templateIds.Add(BaseSerializer.ParsePrimitiveint(_item));
                        }
                    }
                    break;
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<CreateSubmissionFromHtmlRequestDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateSubmissionFromHtmlRequestDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
                case "submitters":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        submitters = new List<CreateSubmissionFromHtmlRequestSubmittersItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateSubmissionFromHtmlRequestSubmittersItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                submitters.Add(_itemValue);
                        }
                    }
                    break;
                case "message":
                    message = CreateSubmissionFromHtmlRequestMessageSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "merge_documents":
                    mergeDocuments = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateSubmissionFromHtmlRequest
        {
            Name = name,
            SendEmail = sendEmail,
            SendSms = sendSms,
            Order = order,
            CompletedRedirectUrl = completedRedirectUrl,
            BccCompleted = bccCompleted,
            ReplyTo = replyTo,
            ExpireAt = expireAt,
            TemplateIds = templateIds,
            Documents = documents!,
            Submitters = submitters!,
            Message = message,
            MergeDocuments = mergeDocuments
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionFromHtmlRequest instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionFromHtmlRequest> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionFromHtmlRequest>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionFromHtmlRequest>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionFromHtmlRequest instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionFromHtmlRequest instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionFromHtmlRequest instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionFromHtmlRequest> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
