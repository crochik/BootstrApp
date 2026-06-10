using Microsoft.Extensions.Options;
using Webhook.Zapier.Configuration;
using Webhook.Zapier.Subscriptions;
using PublisherStore = Webhook.Publisher.Subscriptions.IWebhookSubscriptionStore;
using Xunit;

namespace Webhook.Zapier.Tests;

public class ZapierSubscriptionStoreTests
{
    private static ZapierSubscriptionStore Store(string tenant = "zapier") =>
        new(Options.Create(new ZapierOptions { Tenant = tenant }));

    [Fact]
    public async Task Add_maps_object_event_to_publisher_subscription_under_the_tenant()
    {
        var store = Store("acme");
        store.Add("deal", "won", "https://hooks.zapier.com/a");

        PublisherStore publisher = store;
        var matches = await publisher.GetForAsync("acme", "deal.won");

        Assert.Single(matches);
        Assert.Equal("https://hooks.zapier.com/a", matches[0].Url);
        Assert.Equal("acme", matches[0].TenantId);
        Assert.Contains("deal.won", matches[0].Events);
        Assert.False(string.IsNullOrEmpty(matches[0].Secret)); // signed deliveries
    }

    [Fact]
    public async Task Publisher_view_only_returns_matching_event_name()
    {
        var store = Store();
        store.Add("deal", "won", "https://hooks.zapier.com/a");
        store.Add("deal", "lost", "https://hooks.zapier.com/b");

        PublisherStore publisher = store;
        Assert.Single(await publisher.GetForAsync("zapier", "deal.won"));
        Assert.Empty(await publisher.GetForAsync("zapier", "contact.created"));
    }

    [Fact]
    public async Task Publisher_view_ignores_other_tenants()
    {
        var store = Store("tenant-a");
        store.Add("deal", "won", "https://hooks.zapier.com/a");

        PublisherStore publisher = store;
        Assert.Empty(await publisher.GetForAsync("tenant-b", "deal.won"));
    }

    [Fact]
    public async Task Remove_deletes_from_both_views()
    {
        var store = Store();
        var sub = store.Add("task", "completed", "https://hooks.zapier.com/x");

        Assert.True(store.Remove(sub.Id));

        PublisherStore publisher = store;
        Assert.Empty(await publisher.GetForAsync("zapier", "task.completed"));
        Assert.Empty(store.Find("task", "completed"));
        Assert.False(store.Remove(sub.Id)); // idempotent
    }

    [Fact]
    public void Find_returns_the_zapier_view_for_the_object_event()
    {
        var store = Store();
        store.Add("deal", "won", "https://hooks.zapier.com/a");
        store.Add("contact", "created", "https://hooks.zapier.com/b");

        var found = store.Find("deal", "won");

        Assert.Single(found);
        Assert.Equal("deal", found[0].ObjectKey);
        Assert.Equal("won", found[0].EventKey);
    }
}
