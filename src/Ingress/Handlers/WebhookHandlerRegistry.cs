namespace Ingress.Handlers;

/// <summary>
/// Default registry that indexes all <see cref="IWebhookHandler"/> instances
/// registered in DI by their <see cref="IWebhookHandler.Name"/>.
/// </summary>
public sealed class WebhookHandlerRegistry : IWebhookHandlerRegistry
{
    private readonly IReadOnlyDictionary<string, IWebhookHandler> _handlers;

    public WebhookHandlerRegistry(IEnumerable<IWebhookHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IWebhookHandler? Resolve(string name) =>
        _handlers.TryGetValue(name, out var handler) ? handler : null;
}
