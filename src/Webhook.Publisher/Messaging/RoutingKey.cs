namespace Webhook.Publisher.Messaging;

/// <summary>
/// Builds and parses the topic routing key <c>webhook.{tenantId}.{eventName}</c>.
/// Tenant and event segments are sanitized so they cannot smuggle topic wildcards
/// (<c>*</c>, <c>#</c>) or the segment separator (<c>.</c>).
/// </summary>
public readonly record struct RoutingKey(string TenantId, string EventName)
{
    public const string Prefix = "webhook";

    public static RoutingKey For(string tenantId, string eventName) => new(tenantId, eventName);

    public string Value => $"{Prefix}.{Sanitize(TenantId)}.{Sanitize(EventName)}";

    /// <summary>Extracts the tenant and event from a delivered routing key, if it is well-formed.</summary>
    public static bool TryParse(string routingKey, out string tenantId, out string eventName)
    {
        tenantId = string.Empty;
        eventName = string.Empty;

        var parts = routingKey.Split('.');
        if (parts.Length < 3 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        tenantId = parts[1];
        // Event names may legitimately contain dots; re-join the remainder.
        eventName = string.Join('.', parts[2..]);
        return tenantId.Length > 0 && eventName.Length > 0;
    }

    private static string Sanitize(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException("Routing key segment must be non-empty.", nameof(segment));
        }

        // Replace AMQP topic separators/wildcards and whitespace with '-' to keep the key well-formed.
        Span<char> buffer = stackalloc char[segment.Length];
        for (var i = 0; i < segment.Length; i++)
        {
            var c = segment[i];
            buffer[i] = c is '.' or '*' or '#' || char.IsWhiteSpace(c) ? '-' : c;
        }

        return new string(buffer);
    }
}
