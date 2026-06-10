using Microsoft.Extensions.Options;
using Webhook.Publisher.Publishing;
using Webhook.Zapier.Configuration;
using Webhook.Zapier.Delivery;
using Xunit;

namespace Webhook.Zapier.Tests;

public class PublisherEventPublisherTests
{
    private sealed class FakeWebhookPublisher : IWebhookPublisher
    {
        public List<(string Tenant, string EventName, object Payload)> Calls { get; } = new();
        public int DeliveriesEnqueued { get; set; } = 3;

        public Task<PublishResult> PublishAsync(string tenantId, string eventName, object payload, CancellationToken ct = default)
        {
            Calls.Add((tenantId, eventName, payload));
            return Task.FromResult(new PublishResult("evt_1", DeliveriesEnqueued, true));
        }
    }

    [Fact]
    public async Task Maps_object_and_event_to_tenant_and_dotted_event_name()
    {
        var fake = new FakeWebhookPublisher();
        var adapter = new PublisherEventPublisher(fake, Options.Create(new ZapierOptions { Tenant = "acme" }));
        var payload = new { id = "deal_1" };

        var enqueued = await adapter.PublishAsync("deal", "won", payload);

        Assert.Equal(3, enqueued);
        var call = Assert.Single(fake.Calls);
        Assert.Equal("acme", call.Tenant);
        Assert.Equal("deal.won", call.EventName);
        Assert.Same(payload, call.Payload);
    }

    [Fact]
    public async Task Returns_the_publisher_enqueued_count()
    {
        var fake = new FakeWebhookPublisher { DeliveriesEnqueued = 0 };
        var adapter = new PublisherEventPublisher(fake, Options.Create(new ZapierOptions()));

        Assert.Equal(0, await adapter.PublishAsync("contact", "created", new { }));
    }
}
