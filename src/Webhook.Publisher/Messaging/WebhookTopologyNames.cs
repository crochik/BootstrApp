namespace Webhook.Publisher.Messaging;

/// <summary>
/// Central derivation of every broker object name from the configured exchange
/// prefix, so the publisher, the topology initializer and the worker stay in sync.
/// </summary>
public sealed class WebhookTopologyNames
{
    private readonly string _prefix;

    public WebhookTopologyNames(string exchangePrefix)
    {
        _prefix = string.IsNullOrWhiteSpace(exchangePrefix) ? "webhook" : exchangePrefix;
    }

    /// <summary>Topic exchange the publisher targets; routing key <c>webhook.{tenant}.{event}</c>.</summary>
    public string DeliveryExchange => $"{_prefix}.delivery";

    /// <summary>Direct exchange that feeds the tiered delay ("wait room") queues.</summary>
    public string RetryExchange => $"{_prefix}.retry";

    /// <summary>Main work queue; bound to the delivery exchange with <c>{prefix}.#</c>.</summary>
    public string DeliveryQueue => $"{_prefix}.delivery.q";

    /// <summary>
    /// Binding pattern that routes all freshly published tenants/events into the main queue.
    /// Keyed off the routing-key prefix (not the exchange prefix), since routing keys are always
    /// <c>webhook.{tenant}.{event}</c> regardless of how the exchanges/queues are named.
    /// </summary>
    public string DeliveryBindingPattern => $"{RoutingKey.Prefix}.#";

    /// <summary>
    /// Binding pattern that catches retried messages coming back from the delay tiers.
    /// A retried message carries the <c>retry.{tier}</c> key it was republished with, so the
    /// delivery queue must bind this too; tenant/event are recovered from MongoDB, not the key.
    /// </summary>
    public string RetryComebackPattern => "retry.#";

    /// <summary>Delay queue for retry tier <paramref name="tier"/>.</summary>
    public string RetryQueue(int tier) => $"{_prefix}.retry.{tier}.q";

    /// <summary>Routing key that places a message into the tier <paramref name="tier"/> delay queue.</summary>
    public string RetryRoutingKey(int tier) => $"retry.{tier}";
}
