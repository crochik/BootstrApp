using System.Security.Cryptography;
using System.Text;
using Ingress.Configuration;
using Ingress.Validation;
using Xunit;

namespace Ingress.Tests;

public class SignedTimestampValidatorTests
{
    private const string Timestamp = "1700000000";
    private const string Body = "{\"id\":\"evt_1\"}";

    private static string HexHmac(string secret, string message)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
    }

    [Fact]
    public void Stripe_accepts_valid_signature()
    {
        const string secret = "whsec_test";
        var sig = HexHmac(secret, $"{Timestamp}.{Body}");
        var config = new AuthConfig { Type = "stripe", Header = "Stripe-Signature", Secret = secret };
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body,
            headers: new Dictionary<string, string> { ["Stripe-Signature"] = $"t={Timestamp},v1={sig}" });

        Assert.True(new SignedTimestampValidator("stripe").Validate(context, config).Succeeded);
    }

    [Fact]
    public void Stripe_rejects_tampered_body()
    {
        const string secret = "whsec_test";
        var sig = HexHmac(secret, $"{Timestamp}.{Body}");
        var config = new AuthConfig { Type = "stripe", Secret = secret };
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body + " ",
            headers: new Dictionary<string, string> { ["Stripe-Signature"] = $"t={Timestamp},v1={sig}" });

        Assert.False(new SignedTimestampValidator("stripe").Validate(context, config).Succeeded);
    }

    [Fact]
    public void DocuSeal_accepts_valid_signature()
    {
        const string secret = "whsec_docuseal";
        var sig = HexHmac(secret, $"{Timestamp}.{Body}");
        var config = new AuthConfig { Type = "docuseal", Secret = secret };
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body,
            headers: new Dictionary<string, string> { ["X-Docuseal-Signature"] = $"{Timestamp}.{sig}" });

        Assert.True(new SignedTimestampValidator("docuseal").Validate(context, config).Succeeded);
    }

    [Fact]
    public void OpenPhone_accepts_valid_signature_with_base64_key()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var secretB64 = Convert.ToBase64String(keyBytes);
        using var hmac = new HMACSHA256(keyBytes);
        var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{Timestamp}.{Body}")));

        var config = new AuthConfig { Type = "openphone", Secret = secretB64 };
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body,
            headers: new Dictionary<string, string> { ["openphone-signature"] = $"hmac;1;{Timestamp};{sig}" });

        Assert.True(new SignedTimestampValidator("openphone").Validate(context, config).Succeeded);
    }

    [Fact]
    public void OpenPhone_rejects_wrong_signature()
    {
        var secretB64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var config = new AuthConfig { Type = "openphone", Secret = secretB64 };
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body,
            headers: new Dictionary<string, string> { ["openphone-signature"] = $"hmac;1;{Timestamp};{Convert.ToBase64String(new byte[32])}" });

        Assert.False(new SignedTimestampValidator("openphone").Validate(context, config).Succeeded);
    }
}
