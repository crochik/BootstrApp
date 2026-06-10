using System.Text;
using MongoDB.Bson;
using Webhook.Publisher.Delivery;
using Webhook.Publisher.Messaging;
using Webhook.Publisher.Storage;
using Xunit;

namespace Webhook.Publisher.Tests;

public class MessagingTests
{
    [Fact]
    public void DeliveryMessage_round_trips()
    {
        var bytes = new DeliveryMessage("abc123").Serialize();
        var parsed = DeliveryMessage.Deserialize(bytes);

        Assert.NotNull(parsed);
        Assert.Equal("abc123", parsed!.DeliveryId);
    }

    [Fact]
    public void DeliveryMessage_tolerates_plain_string_body()
    {
        var parsed = DeliveryMessage.Deserialize(Encoding.UTF8.GetBytes("\"legacy-id\""));
        Assert.Equal("legacy-id", parsed!.DeliveryId);
    }

    [Fact]
    public void TopologyNames_derive_from_prefix()
    {
        var names = new WebhookTopologyNames("wh");

        Assert.Equal("wh.delivery", names.DeliveryExchange);
        Assert.Equal("wh.retry", names.RetryExchange);
        Assert.Equal("wh.delivery.q", names.DeliveryQueue);
        Assert.Equal("webhook.#", names.DeliveryBindingPattern);
        Assert.Equal("retry.#", names.RetryComebackPattern);
        Assert.Equal("wh.retry.2.q", names.RetryQueue(2));
        Assert.Equal("retry.2", names.RetryRoutingKey(2));
    }

    [Fact]
    public void WebhookPayload_wraps_data_in_envelope()
    {
        var evt = new WebhookEvent
        {
            Id = "evt1",
            TenantId = "t1",
            EventName = "order.created",
            OccurredAt = DateTime.UtcNow,
            Payload = BsonDocument.Parse("{\"orderId\":123,\"ok\":true}"),
            SchemaVersion = "1",
        };

        var json = Encoding.UTF8.GetString(WebhookPayload.Build(evt));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("evt1", root.GetProperty("eventId").GetString());
        Assert.Equal("t1", root.GetProperty("tenantId").GetString());
        Assert.Equal("order.created", root.GetProperty("eventName").GetString());
        Assert.Equal(123, root.GetProperty("data").GetProperty("orderId").GetInt32());
        Assert.True(root.GetProperty("data").GetProperty("ok").GetBoolean());
    }
}
