using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Webhook.Zapier.Tests;

public class ZapierEndpointTests : IClassFixture<ZapierAppFactory>
{
    private readonly ZapierAppFactory _factory;

    public ZapierEndpointTests(ZapierAppFactory factory) => _factory = factory;

    // Client carrying the demo API key shipped in appsettings.json.
    private HttpClient AuthedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "demo-secret-key");
        return client;
    }

    [Fact]
    public async Task Health_is_open_without_a_key()
    {
        var response = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Zapier_routes_require_a_key()
    {
        var response = await _factory.CreateClient().GetAsync("/zapier/objects");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_key_is_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "not-the-key");

        var response = await client.GetAsync("/zapier/objects");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_connection_name()
    {
        var doc = await GetJson(await AuthedClient().GetAsync("/zapier/me"));
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Objects_endpoint_lists_discovered_objects()
    {
        var doc = await GetJson(await AuthedClient().GetAsync("/zapier/objects"));

        var keys = doc.RootElement.EnumerateArray()
            .Select(o => o.GetProperty("key").GetString())
            .ToList();

        Assert.Contains("contact", keys);
        Assert.Contains("deal", keys);
    }

    [Fact]
    public async Task Events_endpoint_lists_object_events()
    {
        var doc = await GetJson(await AuthedClient().GetAsync("/zapier/objects/deal/events"));

        var keys = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("key").GetString())
            .ToList();

        Assert.Contains("won", keys);
        Assert.Contains("lost", keys);
    }

    [Fact]
    public async Task Subscribe_rejects_unknown_event()
    {
        var response = await AuthedClient().PostAsJsonAsync("/zapier/subscriptions",
            new { @object = "deal", @event = "exploded", targetUrl = "https://hooks.zapier.com/x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Subscribe_rejects_non_http_target()
    {
        var response = await AuthedClient().PostAsJsonAsync("/zapier/subscriptions",
            new { @object = "deal", @event = "won", targetUrl = "ftp://example.com/x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Subscribe_then_unsubscribe_round_trips()
    {
        var client = AuthedClient();

        var subscribe = await client.PostAsJsonAsync("/zapier/subscriptions",
            new { @object = "deal", @event = "won", targetUrl = "https://hooks.zapier.com/abc" });
        Assert.Equal(HttpStatusCode.OK, subscribe.StatusCode);

        var id = (await GetJson(subscribe)).RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrEmpty(id));

        var unsubscribe = await client.DeleteAsync($"/zapier/subscriptions/{id}");
        Assert.Equal(HttpStatusCode.NoContent, unsubscribe.StatusCode);
    }

    [Fact]
    public async Task Samples_endpoint_returns_the_delivered_envelope()
    {
        var doc = await GetJson(await AuthedClient().GetAsync("/zapier/objects/contact/events/created/samples"));

        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        var first = doc.RootElement[0];
        Assert.Equal("contact.created", first.GetProperty("eventName").GetString());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("eventId").GetString()));
        Assert.Equal(JsonValueKind.Object, first.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Emit_publishes_through_the_pipeline()
    {
        var before = _factory.Publisher.Published.Count;

        var response = await AuthedClient().PostAsJsonAsync("/zapier/mock/emit",
            new { @object = "deal", @event = "won" });
        var doc = await GetJson(response);

        Assert.Equal(1, doc.RootElement.GetProperty("enqueued").GetInt32());
        Assert.Equal("deal.won", doc.RootElement.GetProperty("eventName").GetString());

        var published = _factory.Publisher.Published;
        Assert.True(published.Count > before);
        Assert.Equal("deal.won", published[^1].EventName);
        Assert.Equal("zapier", published[^1].Tenant);
    }

    private static async Task<JsonDocument> GetJson(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
