using System.Security.Cryptography;
using System.Text;
using Webhook.Service.Configuration;
using Webhook.Service.Validation;
using Xunit;

namespace Webhook.Service.Tests;

public class SlackTemplateHmacTests
{
    [Fact]
    public void Validates_slack_v0_template_signature()
    {
        const string secret = "slack-signing-secret";
        const string timestamp = "1531420618";
        const string body = "token=abc&team_id=T1";
        var basestring = $"v0:{timestamp}:{body}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = "v0=" + Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(basestring))).ToLowerInvariant();

        var config = new AuthConfig
        {
            Type = "hmac",
            Header = "X-Slack-Signature",
            Algorithm = "sha256",
            Encoding = "hex",
            Prefix = "v0=",
            Template = "v0:{timestamp}:{body}",
            TimestampHeader = "X-Slack-Request-Timestamp",
            Secret = secret
        };
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: body,
            headers: new Dictionary<string, string>
            {
                ["X-Slack-Signature"] = signature,
                ["X-Slack-Request-Timestamp"] = timestamp
            });

        Assert.True(new HmacSignatureValidator().Validate(context, config).Succeeded);
    }

    [Fact]
    public void Rejects_slack_signature_with_wrong_timestamp()
    {
        const string secret = "slack-signing-secret";
        const string body = "token=abc";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = "v0=" + Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes($"v0:111:{body}"))).ToLowerInvariant();

        var config = new AuthConfig
        {
            Type = "hmac", Header = "X-Slack-Signature", Prefix = "v0=",
            Template = "v0:{timestamp}:{body}", TimestampHeader = "X-Slack-Request-Timestamp", Secret = secret
        };
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: body,
            headers: new Dictionary<string, string>
            {
                ["X-Slack-Signature"] = signature,
                ["X-Slack-Request-Timestamp"] = "222"
            });

        Assert.False(new HmacSignatureValidator().Validate(context, config).Succeeded);
    }
}
