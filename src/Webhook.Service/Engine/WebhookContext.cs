using Webhook.Service.Configuration;

namespace Webhook.Service.Engine;

/// <summary>
/// Normalized view of an incoming webhook request, passed to validators, the
/// registration handshake, parsers and the resolved handler. Decoupling this from
/// <c>HttpRequest</c> keeps the pipeline components easy to unit test.
/// </summary>
public sealed class WebhookContext
{
    public required WebhookDefinition Definition { get; init; }

    /// <summary>HTTP method of the incoming request (GET, POST, ...).</summary>
    public required string Method { get; init; }

    /// <summary>Exact raw request body bytes (used for HMAC and raw parsing).</summary>
    public required byte[] RawBody { get; init; }

    /// <summary>Request headers (case-insensitive).</summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    /// <summary>Query string values (case-insensitive).</summary>
    public required IReadOnlyDictionary<string, string> Query { get; init; }

    /// <summary>Remote IP address as seen by the server, if available.</summary>
    public string? RemoteIp { get; init; }

    /// <summary>
    /// Absolute request URL as seen by the server (scheme, host, path, query).
    /// Required by signature schemes that sign the URL, e.g. Twilio.
    /// </summary>
    public string? RequestUrl { get; init; }

    /// <summary>
    /// Client certificate presented during the TLS handshake, if any. Populated
    /// only when the server is configured to negotiate client certificates.
    /// Used by mutual-TLS validation (e.g. Kubernetes admission, Salesforce).
    /// </summary>
    public System.Security.Cryptography.X509Certificates.X509Certificate2? ClientCertificate { get; init; }

    /// <summary>
    /// Parsed payload produced by the configured <c>IPayloadParser</c>. For JSON this
    /// is a <c>JsonElement</c>; for form data a string dictionary; for raw a byte[].
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>Raw body decoded as UTF-8 text (lazily materialized).</summary>
    public string BodyText => _bodyText ??= System.Text.Encoding.UTF8.GetString(RawBody);
    private string? _bodyText;

    public string GetHeader(string name) =>
        Headers.TryGetValue(name, out var value) ? value : string.Empty;
}
