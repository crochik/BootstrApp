using Webhook.Zapier.Catalog;
using Webhook.Zapier.Mock;
using Xunit;

namespace Webhook.Zapier.Tests;

public class ReflectionEventCatalogTests
{
    private static ReflectionEventCatalog Catalog() =>
        new(new[] { typeof(MockContact).Assembly });

    [Fact]
    public void Discovers_mock_objects_from_attributes()
    {
        var keys = Catalog().GetObjects().Select(o => o.Key).ToList();

        Assert.Contains("contact", keys);
        Assert.Contains("deal", keys);
        Assert.Contains("task", keys);
    }

    [Fact]
    public void Object_without_declared_events_gets_default_lifecycle()
    {
        Assert.True(Catalog().TryGetObject("contact", out var contact));

        var eventKeys = contact!.Events.Select(e => e.Key).ToList();
        Assert.Equal(new[] { "created", "updated", "deleted" }, eventKeys);
    }

    [Fact]
    public void Object_with_declared_events_exposes_exactly_those()
    {
        Assert.True(Catalog().TryGetObject("deal", out var deal));

        var eventKeys = deal!.Events.Select(e => e.Key).ToList();
        Assert.Equal(new[] { "created", "stage_changed", "won", "lost" }, eventKeys);
    }

    [Fact]
    public void TryGetEvent_is_case_insensitive_and_resolves_labels()
    {
        Assert.True(Catalog().TryGetEvent("DEAL", "WON", out var won));
        Assert.Equal("won", won!.Key);
        Assert.Equal("Deal Won", won.Label);
    }

    [Fact]
    public void Unknown_object_or_event_returns_false()
    {
        var catalog = Catalog();
        Assert.False(catalog.TryGetObject("nope", out _));
        Assert.False(catalog.TryGetEvent("deal", "exploded", out _));
    }

    [Theory]
    [InlineData("Deal", "deal")]
    [InlineData("stage changed", "stage_changed")]
    [InlineData("Stage-Changed", "stage_changed")]
    [InlineData("  won  ", "won")]
    public void Normalize_produces_url_safe_keys(string input, string expected) =>
        Assert.Equal(expected, ReflectionEventCatalog.Normalize(input));

    [Theory]
    [InlineData("MockContactRecord", "Mock Contact Record")]
    [InlineData("Deal", "Deal")]
    public void Humanize_splits_pascal_case(string input, string expected) =>
        Assert.Equal(expected, ReflectionEventCatalog.Humanize(input));
}
