using System.Text.Json;
using Ingress.Engine;

namespace Ingress.Formats;

/// <summary>Parses the body as JSON into a <see cref="JsonElement"/>.</summary>
public sealed class JsonPayloadParser : IPayloadParser
{
    public string Format => "json";

    public object? Parse(WebhookContext context)
    {
        if (context.RawBody.Length == 0)
        {
            return null;
        }

        using var document = JsonDocument.Parse(context.RawBody);
        // Clone so the element survives disposal of the document.
        return document.RootElement.Clone();
    }
}
