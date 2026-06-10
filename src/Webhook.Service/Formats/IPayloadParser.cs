using Webhook.Service.Engine;

namespace Webhook.Service.Formats;

/// <summary>Parses a raw request body into a structured payload per format.</summary>
public interface IPayloadParser
{
    /// <summary>Format discriminator: <c>json</c>, <c>form</c>, <c>xml</c>, <c>raw</c>.</summary>
    string Format { get; }

    /// <summary>Parses <see cref="WebhookContext.RawBody"/> and returns the payload object.</summary>
    object? Parse(WebhookContext context);
}
