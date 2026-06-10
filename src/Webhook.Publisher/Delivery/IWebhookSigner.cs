namespace Webhook.Publisher.Delivery;

/// <summary>
/// Produces the signature header value sent with each delivery so subscribers can
/// verify authenticity and reject replays.
/// </summary>
public interface IWebhookSigner
{
    /// <summary>
    /// Returns a versioned, timestamped signature header value of the form
    /// <c>t={unixSeconds},v1={hex}</c> over <c>"{unixSeconds}.{body}"</c>.
    /// </summary>
    string Sign(ReadOnlySpan<byte> payload, string secret, DateTimeOffset timestamp);
}
