using Microsoft.Extensions.Options;
using Webhook.Integrations.Core.Configuration;
using Webhook.Integrations.Core.Subscriptions;
using IWebhookPublisher = Webhook.Publisher.Publishing.IWebhookPublisher;

namespace Webhook.Integrations.Core.Delivery;

/// <summary>
/// Adapts the integration <c>(object, event)</c> vocabulary onto the
/// <c>Webhook.Publisher</c>, which speaks <c>(tenantId, eventName)</c>. Publishing
/// here records the event in MongoDB and enqueues a durable, signed, retried HTTP
/// delivery to every subscribed callback URL.
/// </summary>
public sealed class PublisherEventPublisher : IEventPublisher
{
    private readonly IWebhookPublisher _publisher;
    private readonly IOptions<IntegrationOptions> _options;

    public PublisherEventPublisher(IWebhookPublisher publisher, IOptions<IntegrationOptions> options)
    {
        _publisher = publisher;
        _options = options;
    }

    public async Task<int> PublishAsync(string objectKey, string eventKey, object payload, CancellationToken ct = default)
    {
        var eventName = IntegrationSubscriptionStore.EventName(objectKey, eventKey);
        var result = await _publisher.PublishAsync(_options.Value.Tenant, eventName, payload, ct);
        return result.DeliveriesEnqueued;
    }
}
