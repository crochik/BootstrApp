using Ingress.Engine;

namespace Ingress.Formats;

/// <summary>Pass-through parser: exposes the raw body bytes unchanged.</summary>
public sealed class RawPayloadParser : IPayloadParser
{
    public string Format => "raw";

    public object? Parse(WebhookContext context) => context.RawBody;
}
