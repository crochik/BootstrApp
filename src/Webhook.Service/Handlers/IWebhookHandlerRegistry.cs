namespace Webhook.Service.Handlers;

/// <summary>Resolves a registered <see cref="IWebhookHandler"/> by name.</summary>
public interface IWebhookHandlerRegistry
{
    /// <summary>Returns the handler registered under <paramref name="name"/>, or null.</summary>
    IWebhookHandler? Resolve(string name);
}
