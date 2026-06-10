namespace Webhook.Publisher.Messaging;

/// <summary>
/// Declares the exchanges, the main delivery queue and the tiered delay queues
/// exactly once at startup. All declarations are idempotent.
/// </summary>
public interface IWebhookTopologyInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
