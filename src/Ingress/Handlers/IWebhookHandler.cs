using Ingress.Engine;

namespace Ingress.Handlers;

/// <summary>
/// Business logic invoked for a validated, parsed webhook delivery. Consumers add
/// new behaviour by implementing this interface and registering it under a name
/// (see <c>IWebhookHandlerRegistry</c>); the webhook config then references that
/// name via <c>WebhookDefinition.Handler</c>. This is the one extension point
/// that requires code — everything else is configuration.
/// </summary>
public interface IWebhookHandler
{
    /// <summary>Name this handler is registered/dispatched under (e.g. "logging").</summary>
    string Name { get; }

    Task<WebhookResult> HandleAsync(WebhookContext context, CancellationToken cancellationToken = default);
}
