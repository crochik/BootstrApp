using Webhook.Integrations.Core.Catalog;
using Webhook.Integrations.Core.Delivery;

namespace Webhook.Integrations.Core.Mock;

/// <summary>
/// Convenience helper that fabricates a realistic object body using the sample
/// factory and publishes it through <see cref="IEventPublisher"/> (and thus the
/// durable Webhook.Publisher pipeline). Lets a demo enqueue an end-to-end delivery
/// without a real domain backing it.
/// </summary>
public sealed class MockEventEmitter
{
    private readonly ISampleFactory _samples;
    private readonly IEventPublisher _publisher;

    public MockEventEmitter(ISampleFactory samples, IEventPublisher publisher)
    {
        _samples = samples;
        _publisher = publisher;
    }

    /// <summary>
    /// Emits a freshly-generated event for the given object/event. The returned tuple
    /// reports how many per-subscription deliveries were enqueued and the data body
    /// that was published (the publisher wraps it in the delivery envelope).
    /// </summary>
    public async Task<(int Enqueued, IDictionary<string, object?> Data)> EmitAsync(
        TriggerObjectDescriptor descriptor, string eventKey, CancellationToken ct = default)
    {
        var data = _samples.CreateData(descriptor);
        var enqueued = await _publisher.PublishAsync(descriptor.Key, eventKey, data, ct);
        return (enqueued, data);
    }
}
