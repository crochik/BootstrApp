using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Webhook.Publisher.Subscriptions;
using Xunit;

namespace Webhook.Publisher.Tests;

public class SubscriptionStoreTests
{
    private static JsonFileWebhookSubscriptionStore BuildStore()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebhookSubscriptions:Subscriptions:0:Id"] = "s1",
                ["WebhookSubscriptions:Subscriptions:0:TenantId"] = "t1",
                ["WebhookSubscriptions:Subscriptions:0:Url"] = "https://a.example/hook",
                ["WebhookSubscriptions:Subscriptions:0:Enabled"] = "true",
                ["WebhookSubscriptions:Subscriptions:0:Events:0"] = "order.created",

                ["WebhookSubscriptions:Subscriptions:1:Id"] = "s2",
                ["WebhookSubscriptions:Subscriptions:1:TenantId"] = "t1",
                ["WebhookSubscriptions:Subscriptions:1:Url"] = "https://b.example/hook",
                ["WebhookSubscriptions:Subscriptions:1:Enabled"] = "true",
                ["WebhookSubscriptions:Subscriptions:1:Events:0"] = "*",

                ["WebhookSubscriptions:Subscriptions:2:Id"] = "s3",
                ["WebhookSubscriptions:Subscriptions:2:TenantId"] = "t1",
                ["WebhookSubscriptions:Subscriptions:2:Url"] = "https://disabled.example/hook",
                ["WebhookSubscriptions:Subscriptions:2:Enabled"] = "false",

                ["WebhookSubscriptions:Subscriptions:3:Id"] = "s4",
                ["WebhookSubscriptions:Subscriptions:3:TenantId"] = "t2",
                ["WebhookSubscriptions:Subscriptions:3:Url"] = "https://other-tenant.example/hook",
                ["WebhookSubscriptions:Subscriptions:3:Enabled"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<WebhookSubscriptionOptions>(config.GetSection(WebhookSubscriptionOptions.SectionName));
        var provider = services.BuildServiceProvider();

        return new JsonFileWebhookSubscriptionStore(provider.GetRequiredService<IOptionsMonitor<WebhookSubscriptionOptions>>());
    }

    [Fact]
    public async Task Returns_matching_and_wildcard_subscriptions_for_tenant()
    {
        var store = BuildStore();

        var matches = await store.GetForAsync("t1", "order.created");

        Assert.Equal(new[] { "s1", "s2" }, matches.Select(s => s.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task Wildcard_only_matches_unrelated_event()
    {
        var store = BuildStore();

        var matches = await store.GetForAsync("t1", "user.updated");

        Assert.Equal(new[] { "s2" }, matches.Select(s => s.Id));
    }

    [Fact]
    public async Task Hides_disabled_and_other_tenant_subscriptions()
    {
        var store = BuildStore();

        var matches = await store.GetForAsync("t1", "order.created");

        Assert.DoesNotContain(matches, s => s.Id is "s3" or "s4");
    }

    [Fact]
    public async Task InMemory_store_filters_the_same_way()
    {
        var store = new InMemoryWebhookSubscriptionStore(new[]
        {
            new WebhookSubscription { Id = "a", TenantId = "t1", Enabled = true, Events = { "x" } },
            new WebhookSubscription { Id = "b", TenantId = "t1", Enabled = false, Events = { "x" } },
        });

        var matches = await store.GetForAsync("t1", "x");

        Assert.Equal(new[] { "a" }, matches.Select(s => s.Id));
    }
}
