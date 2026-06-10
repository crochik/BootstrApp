using Webhook.Publisher.Messaging;
using Xunit;

namespace Webhook.Publisher.Tests;

public class RoutingKeyTests
{
    [Fact]
    public void Build_produces_prefixed_three_segment_key()
    {
        Assert.Equal("webhook.tenant1.order-created", RoutingKey.For("tenant1", "order-created").Value);
    }

    [Theory]
    [InlineData("ten.ant", "ev*ent", "webhook.ten-ant.ev-ent")]
    [InlineData("a#b", "c d", "webhook.a-b.c-d")]
    public void Build_sanitizes_topic_wildcards_dots_and_whitespace(string tenant, string evt, string expected)
    {
        Assert.Equal(expected, RoutingKey.For(tenant, evt).Value);
    }

    [Fact]
    public void Build_rejects_empty_segments()
    {
        Assert.Throws<ArgumentException>(() => RoutingKey.For("", "evt").Value);
        Assert.Throws<ArgumentException>(() => RoutingKey.For("tenant", " ").Value);
    }

    [Fact]
    public void TryParse_extracts_tenant_and_event()
    {
        Assert.True(RoutingKey.TryParse("webhook.tenant1.order-created", out var tenant, out var evt));
        Assert.Equal("tenant1", tenant);
        Assert.Equal("order-created", evt);
    }

    [Theory]
    [InlineData("retry.3")]
    [InlineData("nope")]
    [InlineData("webhook.onlytenant")]
    public void TryParse_rejects_malformed_keys(string key)
    {
        Assert.False(RoutingKey.TryParse(key, out _, out _));
    }
}
