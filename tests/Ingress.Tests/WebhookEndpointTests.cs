using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ingress.Config;
using Ingress.Configuration;
using Ingress.DependencyInjection;
using Ingress.Engine;
using Ingress.Responses;
using Xunit;

namespace Ingress.Tests;

/// <summary>
/// Exercises the full engine pipeline in-process — the same flow the
/// <c>WebhookController</c> drives (resolve definition → build context → process) — using an
/// isolated in-memory config store so the assertions are independent of the deployed
/// definitions. Replaces the previous WebApplicationFactory-based HTTP tests, which cannot
/// hook a MicroserviceApp-built host.
/// </summary>
public class WebhookEndpointTests
{
    private readonly IServiceProvider _provider;
    private readonly IWebhookConfigStore _store;

    public WebhookEndpointTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebhookEngine();

        // Swap the Mongo-backed store for a deterministic in-memory set.
        services.RemoveAll<IWebhookConfigStore>();
        services.AddSingleton<IWebhookConfigStore>(new InMemoryWebhookConfigStore(Definitions()));

        _provider = services.BuildServiceProvider();
        _store = _provider.GetRequiredService<IWebhookConfigStore>();
    }

    /// <summary>
    /// Mirrors <c>WebhookController.Receive</c>: looks up the definition (null => 404),
    /// builds the context and runs the processor. Returns null to represent a 404.
    /// </summary>
    private async Task<BuiltResponse?> ReceiveAsync(
        string uuid,
        string method = "POST",
        string body = "",
        IDictionary<string, string>? headers = null,
        IDictionary<string, string>? query = null,
        string? requestUrl = null)
    {
        var definition = await _store.GetByUuidAsync(uuid);
        if (definition is null)
        {
            return null;
        }

        var context = TestContextFactory.Create(definition, body, method, headers, query, requestUrl: requestUrl);
        var processor = _provider.GetRequiredService<WebhookProcessor>();
        return await processor.ProcessAsync(context);
    }

    [Fact]
    public async Task Unknown_uuid_returns_404()
    {
        Assert.Null(await ReceiveAsync("does-not-exist", body: "{}"));
    }

    [Fact]
    public async Task ApiKey_success_uses_json_response_template()
    {
        var response = await ReceiveAsync("apikey-hook",
            body: "{\"x\":1}",
            headers: new Dictionary<string, string> { ["X-Api-Key"] = "k-secret" });

        Assert.NotNull(response);
        Assert.Equal(200, response!.Value.Status);
        Assert.Equal("{\"ok\":true,\"hook\":\"apikey-hook\"}", response.Value.Body);
    }

    [Fact]
    public async Task ApiKey_wrong_key_returns_401()
    {
        var response = await ReceiveAsync("apikey-hook",
            body: "{}",
            headers: new Dictionary<string, string> { ["X-Api-Key"] = "wrong" });

        Assert.NotNull(response);
        Assert.Equal(401, response!.Value.Status);
    }

    [Fact]
    public async Task Hmac_valid_signature_succeeds_and_invalid_fails()
    {
        const string body = "{\"event\":\"push\"}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("hmac-secret"));
        var signature = "sha256=" + Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        var ok = await ReceiveAsync("hmac-hook", body: body,
            headers: new Dictionary<string, string> { ["X-Hub-Signature-256"] = signature });
        Assert.NotNull(ok);
        Assert.Equal(200, ok!.Value.Status);

        var bad = await ReceiveAsync("hmac-hook", body: body,
            headers: new Dictionary<string, string> { ["X-Hub-Signature-256"] = "sha256=bad" });
        Assert.NotNull(bad);
        Assert.Equal(401, bad!.Value.Status);
    }

    [Fact]
    public async Task Meta_style_challenge_query_is_echoed()
    {
        var response = await ReceiveAsync("meta-hook", method: "GET",
            query: new Dictionary<string, string>
            {
                ["hub.mode"] = "subscribe",
                ["hub.verify_token"] = "verify-me",
                ["hub.challenge"] = "12345"
            });

        Assert.NotNull(response);
        Assert.Equal(200, response!.Value.Status);
        Assert.Equal("12345", response.Value.Body);
    }

    [Fact]
    public async Task Meta_style_challenge_with_wrong_token_is_forbidden()
    {
        var response = await ReceiveAsync("meta-hook", method: "GET",
            query: new Dictionary<string, string>
            {
                ["hub.verify_token"] = "nope",
                ["hub.challenge"] = "12345"
            });

        Assert.NotNull(response);
        Assert.Equal(403, response!.Value.Status);
    }

    [Fact]
    public async Task Twilio_form_signature_validates_over_buffered_body()
    {
        const string url = "https://unit.test/webhooks/twilio-hook";

        // Twilio: HMAC-SHA1(base64) over URL + sorted "key+value" pairs.
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes("twilio-token"));
        var signature = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(url + "Body" + "Hi" + "From" + "+15551112222")));

        var response = await ReceiveAsync("twilio-hook",
            body: "From=%2B15551112222&Body=Hi",
            headers: new Dictionary<string, string> { ["X-Twilio-Signature"] = signature },
            requestUrl: url);

        Assert.NotNull(response);
        Assert.Equal(200, response!.Value.Status);
    }

    [Fact]
    public async Task Slack_style_url_verification_echoes_challenge()
    {
        var response = await ReceiveAsync("slack-hook",
            body: "{\"type\":\"url_verification\",\"challenge\":\"abc-challenge\"}");

        Assert.NotNull(response);
        Assert.Equal(200, response!.Value.Status);
        Assert.Equal("abc-challenge", response.Value.Body);
    }

    private static IEnumerable<WebhookDefinition> Definitions() => new[]
    {
        new WebhookDefinition
        {
            Uuid = "apikey-hook", Name = "apikey-hook", Format = "json",
            Auth = { new AuthConfig { Type = "apikey", Header = "X-Api-Key", Token = "k-secret" } },
            Response = { ContentType = "application/json", Body = "{\"ok\":true,\"hook\":\"{{name}}\"}" }
        },
        new WebhookDefinition
        {
            Uuid = "hmac-hook", Name = "hmac-hook",
            Auth = { new AuthConfig { Type = "hmac", Header = "X-Hub-Signature-256", Prefix = "sha256=", Secret = "hmac-secret" } }
        },
        new WebhookDefinition
        {
            Uuid = "meta-hook", Name = "meta-hook",
            Registration = { Mode = "challengeQuery", ChallengeParam = "hub.challenge", VerifyParam = "hub.verify_token", VerifyValue = "verify-me" }
        },
        new WebhookDefinition
        {
            Uuid = "slack-hook", Name = "slack-hook",
            Registration = { Mode = "challengeBody", TriggerField = "type", TriggerValue = "url_verification", ChallengeField = "challenge" }
        },
        new WebhookDefinition
        {
            Uuid = "twilio-hook", Name = "twilio-hook", Format = "form",
            Auth = { new AuthConfig { Type = "twilio", Token = "twilio-token", Url = "https://unit.test/webhooks/twilio-hook" } }
        }
    };
}
