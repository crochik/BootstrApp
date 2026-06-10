using System.Security.Cryptography;
using System.Text;

namespace PI.Shared.Integrations.Delivery;

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

/// <summary>
/// HMAC-SHA256 signer using the Stripe-style <c>t=...,v1=...</c> format. The signed
/// string is <c>"{unixSeconds}.{body}"</c>.
/// </summary>
public sealed class HmacWebhookSigner : IWebhookSigner
{
    public string Sign(ReadOnlySpan<byte> payload, string secret, DateTimeOffset timestamp)
    {
        var unixSeconds = timestamp.ToUnixTimeSeconds();

        var body = Encoding.UTF8.GetString(payload);
        var signingString = $"{unixSeconds}.{body}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret ?? string.Empty));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingString));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();

        return $"t={unixSeconds},v1={hex}";
    }
}
