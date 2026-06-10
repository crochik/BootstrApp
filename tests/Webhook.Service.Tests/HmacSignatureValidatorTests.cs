using System.Security.Cryptography;
using System.Text;
using Webhook.Service.Configuration;
using Webhook.Service.Validation;
using Xunit;

namespace Webhook.Service.Tests;

public class HmacSignatureValidatorTests
{
    private const string Secret = "top-secret";
    private const string Body = "{\"hello\":\"world\"}";

    private static string HexSignature(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void Accepts_valid_signature_with_prefix()
    {
        var config = new AuthConfig
        {
            Type = "hmac", Header = "X-Hub-Signature-256",
            Algorithm = "sha256", Encoding = "hex", Prefix = "sha256=", Secret = Secret
        };
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body,
            headers: new Dictionary<string, string>
            {
                ["X-Hub-Signature-256"] = "sha256=" + HexSignature(Secret, Body)
            });

        var result = new HmacSignatureValidator().Validate(context, config);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Rejects_tampered_signature()
    {
        var config = new AuthConfig
        {
            Type = "hmac", Header = "X-Hub-Signature-256", Secret = Secret, Prefix = "sha256="
        };
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body,
            headers: new Dictionary<string, string>
            {
                ["X-Hub-Signature-256"] = "sha256=deadbeef"
            });

        var result = new HmacSignatureValidator().Validate(context, config);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Rejects_missing_header()
    {
        var config = new AuthConfig { Type = "hmac", Header = "X-Hub-Signature-256", Secret = Secret };
        var context = TestContextFactory.Create(new WebhookDefinition(), body: Body);

        var result = new HmacSignatureValidator().Validate(context, config);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Supports_base64_encoding()
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var b64 = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(Body)));
        var config = new AuthConfig
        {
            Type = "hmac", Header = "X-Signature", Encoding = "base64", Secret = Secret
        };
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body,
            headers: new Dictionary<string, string> { ["X-Signature"] = b64 });

        var result = new HmacSignatureValidator().Validate(context, config);

        Assert.True(result.Succeeded);
    }
}
