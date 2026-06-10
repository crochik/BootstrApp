using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Webhook.Service.Config;
using Webhook.Service.Configuration;
using Xunit;

namespace Webhook.Service.Tests;

public class WebhookEndpointTests : IClassFixture<WebhookEndpointTests.Factory>
{
    private readonly Factory _factory;

    public WebhookEndpointTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Unknown_uuid_returns_404()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/webhooks/does-not-exist", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApiKey_success_uses_json_response_template()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/apikey-hook")
        {
            Content = new StringContent("{\"x\":1}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", "k-secret");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"ok\":true,\"hook\":\"apikey-hook\"}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApiKey_wrong_key_returns_401()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/apikey-hook")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", "wrong");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hmac_valid_signature_succeeds_and_invalid_fails()
    {
        var client = _factory.CreateClient();
        const string body = "{\"event\":\"push\"}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("hmac-secret"));
        var signature = "sha256=" + Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        var ok = new HttpRequestMessage(HttpMethod.Post, "/webhooks/hmac-hook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        ok.Headers.Add("X-Hub-Signature-256", signature);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(ok)).StatusCode);

        var bad = new HttpRequestMessage(HttpMethod.Post, "/webhooks/hmac-hook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        bad.Headers.Add("X-Hub-Signature-256", "sha256=bad");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(bad)).StatusCode);
    }

    [Fact]
    public async Task Meta_style_challenge_query_is_echoed()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            "/webhooks/meta-hook?hub.mode=subscribe&hub.verify_token=verify-me&hub.challenge=12345");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("12345", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Meta_style_challenge_with_wrong_token_is_forbidden()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            "/webhooks/meta-hook?hub.verify_token=nope&hub.challenge=12345");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Twilio_form_signature_validates_over_buffered_body()
    {
        var client = _factory.CreateClient();
        const string url = "https://unit.test/webhooks/twilio-hook";

        // Twilio: HMAC-SHA1(base64) over URL + sorted "key+value" pairs.
        using var hmac = new System.Security.Cryptography.HMACSHA1(Encoding.UTF8.GetBytes("twilio-token"));
        var signature = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(url + "Body" + "Hi" + "From" + "+15551112222")));

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/twilio-hook")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["From"] = "+15551112222",
                ["Body"] = "Hi"
            })
        };
        request.Headers.Add("X-Twilio-Signature", signature);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Slack_style_url_verification_echoes_challenge()
    {
        var client = _factory.CreateClient();
        var body = "{\"type\":\"url_verification\",\"challenge\":\"abc-challenge\"}";

        var response = await client.PostAsync("/webhooks/slack-hook",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("abc-challenge", await response.Content.ReadAsStringAsync());
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Replace the JSON-file store with an isolated in-memory set so the
            // tests are deterministic regardless of the shipped webhooks.json.
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IWebhookConfigStore>();
                services.AddSingleton<IWebhookConfigStore>(new InMemoryWebhookConfigStore(Definitions()));
            });
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
}
