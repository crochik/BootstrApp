using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionFromDocxRequest - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionFromDocxRequestSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionFromDocxRequest instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromDocxRequest? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionFromDocxRequest instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromDocxRequest? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionFromDocxRequest DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? name = default;
        bool? sendEmail = default;
        bool? sendSms = default;
        object? variables = default;
        string? order = default;
        string? completedRedirectUrl = default;
        string? bccCompleted = default;
        string? replyTo = default;
        string? expireAt = default;
        List<int>? templateIds = null;
        List<CreateSubmissionFromDocxRequestDocumentsItem>? documents = null;
        List<CreateSubmissionFromDocxRequestSubmittersItem>? submitters = null;
        CreateSubmissionFromDocxRequestMessage? message = default;
        bool? mergeDocuments = default;
        bool? removeTags = default;

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
                case "variables":
                    variables = BaseSerializer.ParsePrimitiveobject(property.Value);
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
                        documents = new List<CreateSubmissionFromDocxRequestDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateSubmissionFromDocxRequestDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
                case "submitters":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        submitters = new List<CreateSubmissionFromDocxRequestSubmittersItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateSubmissionFromDocxRequestSubmittersItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                submitters.Add(_itemValue);
                        }
                    }
                    break;
                case "message":
                    message = CreateSubmissionFromDocxRequestMessageSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "merge_documents":
                    mergeDocuments = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "remove_tags":
                    removeTags = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateSubmissionFromDocxRequest
        {
            Name = name,
            SendEmail = sendEmail,
            SendSms = sendSms,
            Variables = variables,
            Order = order,
            CompletedRedirectUrl = completedRedirectUrl,
            BccCompleted = bccCompleted,
            ReplyTo = replyTo,
            ExpireAt = expireAt,
            TemplateIds = templateIds,
            Documents = documents!,
            Submitters = submitters!,
            Message = message,
            MergeDocuments = mergeDocuments,
            RemoveTags = removeTags
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionFromDocxRequest instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionFromDocxRequest> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionFromDocxRequest>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionFromDocxRequest>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionFromDocxRequest instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionFromDocxRequest instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionFromDocxRequest instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionFromDocxRequest> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
