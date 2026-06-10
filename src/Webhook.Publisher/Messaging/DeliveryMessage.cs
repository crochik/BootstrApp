using System.Text;
using System.Text.Json;

namespace Webhook.Publisher.Messaging;

/// <summary>
/// The entire wire payload: just a reference to the delivery whose state lives in
/// MongoDB. No event payload ever travels through RabbitMQ.
/// </summary>
public sealed record DeliveryMessage(string DeliveryId)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(this, SerializerOptions);

    public static DeliveryMessage? Deserialize(ReadOnlySpan<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<DeliveryMessage>(body, SerializerOptions);
        }
        catch (JsonException)
        {
            // Tolerate a legacy/plain-string body that is just the delivery id.
            var raw = Encoding.UTF8.GetString(body).Trim().Trim('"');
            return string.IsNullOrEmpty(raw) ? null : new DeliveryMessage(raw);
        }
    }
}
