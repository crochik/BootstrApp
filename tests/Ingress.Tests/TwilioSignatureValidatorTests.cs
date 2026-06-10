using System.Security.Cryptography;
using System.Text;
using Ingress.Configuration;
using Ingress.Validation;
using Xunit;

namespace Ingress.Tests;

public class TwilioSignatureValidatorTests
{
    private const string Token = "twilio-auth-token";
    private const string Url = "https://example.com/webhooks/twilio";

    // Twilio: HMAC-SHA1 of (URL + sorted "key+value" pairs), base64.
    private static string Sign(string url, params (string Key, string Value)[] sorted)
    {
        var builder = new StringBuilder(url);
        foreach (var (key, value) in sorted.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append(key).Append(value);
        }

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(Token));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    [Fact]
    public void Accepts_valid_signature_over_url_and_sorted_form_params()
    {
        var signature = Sign(Url, ("Body", "Hi there"), ("From", "+15557654321"), ("To", "+15551234567"));
        var config = new AuthConfig { Type = "twilio", Token = Token, Url = Url };
        var context = TestContextFactory.Create(
            new WebhookDefinition { Format = "form" },
            body: "To=%2B15551234567&From=%2B15557654321&Body=Hi+there",
            headers: new Dictionary<string, string> { ["X-Twilio-Signature"] = signature });

        Assert.True(new TwilioSignatureValidator().Validate(context, config).Succeeded);
    }

    [Fact]
    public void Rejects_tampered_parameters()
    {
        var signature = Sign(Url, ("Body", "Hi there"), ("From", "+15557654321"), ("To", "+15551234567"));
        var config = new AuthConfig { Type = "twilio", Token = Token, Url = Url };
        var context = TestContextFactory.Create(
            new WebhookDefinition { Format = "form" },
            body: "To=%2B19998887777&From=%2B15557654321&Body=Hi+there",
            headers: new Dictionary<string, string> { ["X-Twilio-Signature"] = signature });

        Assert.False(new TwilioSignatureValidator().Validate(context, config).Succeeded);
    }

    [Fact]
    public void Accepts_json_delivery_via_bodySHA256()
    {
        const string body = "{\"AccountSid\":\"AC1\"}";
        var bodyHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var signingUrl = $"{Url}?bodySHA256={bodyHash}";

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(Token));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingUrl)));

        var config = new AuthConfig { Type = "twilio", Token = Token, Url = Url };
        var context = TestContextFactory.Create(
            new WebhookDefinition { Format = "json" },
            body: body,
            headers: new Dictionary<string, string> { ["X-Twilio-Signature"] = signature },
            query: new Dictionary<string, string> { ["bodySHA256"] = bodyHash });

        Assert.True(new TwilioSignatureValidator().Validate(context, config).Succeeded);
    }

    [Fact]
    public void Rejects_json_delivery_with_tampered_body()
    {
        const string body = "{\"AccountSid\":\"AC1\"}";
        var bodyHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var signingUrl = $"{Url}?bodySHA256={bodyHash}";
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(Token));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingUrl)));

        var config = new AuthConfig { Type = "twilio", Token = Token, Url = Url };
        var context = TestContextFactory.Create(
            new WebhookDefinition { Format = "json" },
            body: "{\"AccountSid\":\"HACKED\"}",
            headers: new Dictionary<string, string> { ["X-Twilio-Signature"] = signature },
            query: new Dictionary<string, string> { ["bodySHA256"] = bodyHash });

        Assert.False(new TwilioSignatureValidator().Validate(context, config).Succeeded);
    }

    [Fact]
    public void Falls_back_to_request_url_when_no_override()
    {
        var signature = Sign(Url, ("A", "1"));
        var config = new AuthConfig { Type = "twilio", Token = Token };
        var context = TestContextFactory.Create(
            new WebhookDefinition { Format = "form" },
            body: "A=1",
            headers: new Dictionary<string, string> { ["X-Twilio-Signature"] = signature },
            requestUrl: Url);

        Assert.True(new TwilioSignatureValidator().Validate(context, config).Succeeded);
    }
}
