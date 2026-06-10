using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionFromPdfRequestSubmittersItem - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionFromPdfRequestSubmittersItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionFromPdfRequestSubmittersItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromPdfRequestSubmittersItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionFromPdfRequestSubmittersItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromPdfRequestSubmittersItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionFromPdfRequestSubmittersItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? name = default;
        string? role = default;
        string? email = default;
        string? phone = default;
        object? values = default;
        string? externalId = default;
        bool? completed = default;
        object? metadata = default;
        bool? sendEmail = default;
        bool? sendSms = default;
        string? replyTo = default;
        string? completedRedirectUrl = default;
        int? order = default;
        bool? requirePhone2fa = default;
        bool? requireEmail2fa = default;
        string? inviteBy = default;
        List<CreateSubmissionFromPdfRequestSubmittersItemFieldsItem>? fields = null;
        List<string>? roles = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "role":
                    role = BaseSerializer.ParsePrimitivestring(property.Value);
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
                case "completed":
                    completed = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "metadata":
                    metadata = BaseSerializer.ParsePrimitiveobject(property.Value);
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
                case "completed_redirect_url":
                    completedRedirectUrl = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "order":
                    order = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "require_phone_2fa":
                    requirePhone2fa = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "require_email_2fa":
                    requireEmail2fa = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "invite_by":
                    inviteBy = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "fields":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        fields = new List<CreateSubmissionFromPdfRequestSubmittersItemFieldsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateSubmissionFromPdfRequestSubmittersItemFieldsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                fields.Add(_itemValue);
                        }
                    }
                    break;
                case "roles":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        roles = new List<string>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            roles.Add(BaseSerializer.ParsePrimitivestring(_item));
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateSubmissionFromPdfRequestSubmittersItem
        {
            Name = name,
            Role = role,
            Email = email,
            Phone = phone,
            Values = values,
            ExternalId = externalId,
            Completed = completed,
            Metadata = metadata,
            SendEmail = sendEmail,
            SendSms = sendSms,
            ReplyTo = replyTo,
            CompletedRedirectUrl = completedRedirectUrl,
            Order = order,
            RequirePhone2Fa = requirePhone2fa,
            RequireEmail2Fa = requireEmail2fa,
            InviteBy = inviteBy,
            Fields = fields,
            Roles = roles
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionFromPdfRequestSubmittersItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionFromPdfRequestSubmittersItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionFromPdfRequestSubmittersItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionFromPdfRequestSubmittersItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionFromPdfRequestSubmittersItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionFromPdfRequestSubmittersItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionFromPdfRequestSubmittersItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionFromPdfRequestSubmittersItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
