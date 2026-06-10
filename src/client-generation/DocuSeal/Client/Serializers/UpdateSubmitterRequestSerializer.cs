using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for UpdateSubmitterRequest - handles serialization and deserialization
/// </summary>
public static class UpdateSubmitterRequestSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a UpdateSubmitterRequest instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateSubmitterRequest? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a UpdateSubmitterRequest instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateSubmitterRequest? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static UpdateSubmitterRequest DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? name = default;
        string? email = default;
        string? phone = default;
        object? values = default;
        string? externalId = default;
        bool? sendEmail = default;
        bool? sendSms = default;
        string? replyTo = default;
        bool? completed = default;
        object? metadata = default;
        string? completedRedirectUrl = default;
        bool? requirePhone2fa = default;
        UpdateSubmitterRequestMessage? message = default;
        List<UpdateSubmitterRequestFieldsItem>? fields = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "email":
                    email = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "phone":
                    phone = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "values":
                    values = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "external_id":
                    externalId = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "send_email":
                    sendEmail = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "send_sms":
                    sendSms = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "reply_to":
                    replyTo = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "completed":
                    completed = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "metadata":
                    metadata = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "completed_redirect_url":
                    completedRedirectUrl = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "require_phone_2fa":
                    requirePhone2fa = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "message":
                    message = UpdateSubmitterRequestMessageSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "fields":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        fields = new List<UpdateSubmitterRequestFieldsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = UpdateSubmitterRequestFieldsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                fields.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new UpdateSubmitterRequest
        {
            Name = name,
            Email = email,
            Phone = phone,
            Values = values,
            ExternalId = externalId,
            SendEmail = sendEmail,
            SendSms = sendSms,
            ReplyTo = replyTo,
            Completed = completed,
            Metadata = metadata,
            CompletedRedirectUrl = completedRedirectUrl,
            RequirePhone2Fa = requirePhone2fa,
            Message = message,
            Fields = fields
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of UpdateSubmitterRequest instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<UpdateSubmitterRequest> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<UpdateSubmitterRequest>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<UpdateSubmitterRequest>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a UpdateSubmitterRequest instance to a JSON string
    /// </summary>
    public static string SerializeToJson(UpdateSubmitterRequest instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of UpdateSubmitterRequest instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<UpdateSubmitterRequest> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
