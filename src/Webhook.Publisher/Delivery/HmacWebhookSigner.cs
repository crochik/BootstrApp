using System.Security.Cryptography;
using System.Text;

namespace Webhook.Publisher.Delivery;

/// <summary>
/// HMAC-SHA256 signer using the Stripe-style <c>t=...,v1=...</c> format. The signed
/// string is <c>"{unixSeconds}.{body}"</c>, which the inbound service's
/// <c>SignedTimestampValidator("stripe")</c> verifies as-is.
/// </summary>
public sealed class HmacWebhookSigner : IWebhookSigner
{
    public string Sign(ReadOnlySpan<byte> payload, string secret, DateTimeOffset timestamp)
    {
        var unixSeconds = timestamp.ToUnixTimeSeconds();

        // signingString = "{t}.{body}"
        var body = Encoding.UTF8.GetString(payload);
        var signingString = $"{unixSeconds}.{body}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingString));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();

        return $"t={unixSeconds},v1={hex}";
    }
}
