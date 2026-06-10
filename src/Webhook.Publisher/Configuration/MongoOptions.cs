namespace Webhook.Publisher.Configuration;

/// <summary>
/// Settings for the MongoDB store that is the source of truth for event payloads
/// and per-delivery status. RabbitMQ only carries a delivery reference.
/// </summary>
public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string Database { get; set; } = "webhooks";

    /// <summary>Collection holding one document per published event (with payload).</summary>
    public string EventsCollection { get; set; } = "webhook_events";

    /// <summary>Collection holding one document per (event × subscription) delivery.</summary>
    public string DeliveriesCollection { get; set; } = "webhook_deliveries";
}
