namespace Webhook.Service.Configuration;

/// <summary>
/// Configuration for a single authentication / validation step. The <see cref="Type"/>
/// determines which other fields are meaningful. All configured steps must pass.
/// </summary>
public sealed class AuthConfig
{
    /// <summary>
    /// Validator discriminator: <c>hmac</c>, <c>bearer</c>, <c>apikey</c>,
    /// <c>basic</c>, <c>ipAllowlist</c> or <c>none</c>.
    /// </summary>
    public string Type { get; set; } = "none";

    // --- hmac ---

    /// <summary>Header carrying the signature (e.g. <c>X-Hub-Signature-256</c>).</summary>
    public string? Header { get; set; }

    /// <summary>Hash algorithm for HMAC: <c>sha256</c> (default) or <c>sha1</c>.</summary>
    public string Algorithm { get; set; } = "sha256";

    /// <summary>Signature encoding: <c>hex</c> (default) or <c>base64</c>.</summary>
    public string Encoding { get; set; } = "hex";

    /// <summary>Optional prefix stripped from the header value, e.g. <c>sha256=</c>.</summary>
    public string? Prefix { get; set; }

    /// <summary>Shared secret used to compute the HMAC.</summary>
    public string? Secret { get; set; }

    /// <summary>
    /// When the secret is itself base64-encoded (decoded to bytes before use as
    /// the HMAC key). Defaults to false (the secret is used as UTF-8 bytes).
    /// </summary>
    public bool SecretIsBase64 { get; set; }

    /// <summary>
    /// Signing-base template for HMAC. Tokens: <c>{body}</c> and <c>{timestamp}</c>
    /// (read from <see cref="TimestampHeader"/>). Defaults to the raw body. Example
    /// (Slack): <c>v0:{timestamp}:{body}</c>.
    /// </summary>
    public string? Template { get; set; }

    // --- bearer / apikey ---

    /// <summary>
    /// Expected token value. For <c>bearer</c> it is compared against the
    /// <c>Authorization: Bearer &lt;token&gt;</c> header; for <c>apikey</c> against
    /// the configured <see cref="Header"/>.
    /// </summary>
    public string? Token { get; set; }

    // --- basic ---

    /// <summary>Expected username for HTTP Basic auth.</summary>
    public string? Username { get; set; }

    /// <summary>Expected password for HTTP Basic auth.</summary>
    public string? Password { get; set; }

    // --- twilio ---

    /// <summary>
    /// Exact public URL Twilio used when computing the signature. When set it
    /// overrides the server-observed request URL (needed behind proxies / tunnels,
    /// where the host the server sees differs from the configured webhook URL).
    /// </summary>
    public string? Url { get; set; }

    // --- ecdsa (SendGrid) ---

    /// <summary>Header carrying the request timestamp (e.g. <c>X-Twilio-Email-Event-Webhook-Timestamp</c>).</summary>
    public string? TimestampHeader { get; set; }

    /// <summary>Base64 (SubjectPublicKeyInfo) ECDSA public key used to verify the signature.</summary>
    public string? PublicKey { get; set; }

    // --- ipAllowlist ---

    /// <summary>Allowed source IPs / CIDR ranges (e.g. <c>173.252.0.0/16</c>).</summary>
    public List<string> Ranges { get; set; } = new();

    /// <summary>
    /// When true the validator trusts the left-most <c>X-Forwarded-For</c> entry
    /// (use only behind a trusted proxy). Defaults to false.
    /// </summary>
    public bool TrustForwardedFor { get; set; }

    // --- clientCert (mutual TLS, e.g. Kubernetes admission, Salesforce) ---

    /// <summary>Allowed client-certificate SHA-1 thumbprints (colons/spaces ignored, case-insensitive).</summary>
    public List<string> Thumbprints { get; set; } = new();

    /// <summary>Optional required client-certificate subject distinguished name.</summary>
    public string? Subject { get; set; }

    // --- bodyField (e.g. Microsoft Graph clientState) ---

    /// <summary>
    /// Dot-path into the JSON body to validate. A <c>[]</c> segment iterates an
    /// array and requires every element to match, e.g. <c>value[].clientState</c>.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>Expected value at <see cref="Path"/>.</summary>
    public string? Value { get; set; }
}
