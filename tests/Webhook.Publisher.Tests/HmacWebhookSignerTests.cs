using System.Security.Cryptography;
using System.Text;
using Webhook.Publisher.Delivery;
using Webhook.Service.Configuration;
using Webhook.Service.Engine;
using Webhook.Service.Validation;
using Xunit;

namespace Webhook.Publisher.Tests;

public class HmacWebhookSignerTests
{
    private readonly HmacWebhookSigner _signer = new();

    [Fact]
    public void Sign_emits_stripe_style_versioned_header()
    {
        var body = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
        var ts = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

        var header = _signer.Sign(body, "shh", ts);

        Assert.StartsWith("t=1700000000,v1=", header);

        // Independently recompute the expected HMAC over "{t}.{body}".
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("shh"));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"1700000000.{Encoding.UTF8.GetString(body)}"))).ToLowerInvariant();
        Assert.Equal($"t=1700000000,v1={expected}", header);
    }

    [Fact]
    public void Signature_verifies_with_inbound_stripe_validator()
    {
        const string secret = "whsec_test_secret";
        var body = Encoding.UTF8.GetBytes("{\"event\":\"order.created\",\"id\":42}");
        var ts = DateTimeOffset.UtcNow;

        var header = _signer.Sign(body, secret, ts);

        // Feed our outbound signature into the existing inbound validator.
        var validator = new SignedTimestampValidator("stripe");
        var context = new WebhookContext
        {
            Definition = new WebhookDefinition(),
            Method = "POST",
            RawBody = body,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Stripe-Signature"] = header,
            },
            Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
        var config = new AuthConfig { Type = "stripe", Secret = secret, Header = "Stripe-Signature" };

        var result = validator.Validate(context, config);

        Assert.True(result.Succeeded, result.Reason);
    }

    [Fact]
    public void Tampered_body_fails_inbound_validation()
    {
        const string secret = "whsec_test_secret";
        var body = Encoding.UTF8.GetBytes("{\"amount\":100}");
        var header = _signer.Sign(body, secret, DateTimeOffset.UtcNow);

        var validator = new SignedTimestampValidator("stripe");
        var context = new WebhookContext
        {
            Definition = new WebhookDefinition(),
            Method = "POST",
            RawBody = Encoding.UTF8.GetBytes("{\"amount\":999}"), // tampered
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Stripe-Signature"] = header },
            Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
        var config = new AuthConfig { Type = "stripe", Secret = secret, Header = "Stripe-Signature" };

        Assert.False(validator.Validate(context, config).Succeeded);
    }
}
