namespace Webhook.Publisher.Configuration;

/// <summary>
/// Root options for the outbound webhook publisher, bound from the
/// <c>WebhookPublisher</c> configuration section.
/// </summary>
public sealed class WebhookPublisherOptions
{
    public const string SectionName = "WebhookPublisher";

    public RabbitMqOptions RabbitMq { get; set; } = new();

    public MongoOptions Mongo { get; set; } = new();

    public RetryOptions Retry { get; set; } = new();

    public DeliveryOptions Delivery { get; set; } = new();
}
