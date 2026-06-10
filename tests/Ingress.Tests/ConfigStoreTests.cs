using Ingress.Config;
using Ingress.Configuration;
using Xunit;

namespace Ingress.Tests;

public class ConfigStoreTests
{
    [Fact]
    public async Task InMemory_store_resolves_enabled_definition_case_insensitively()
    {
        var store = new InMemoryWebhookConfigStore(new[]
        {
            new WebhookDefinition { Uuid = "uuid-1", Name = "first", Enabled = true }
        });

        var found = await store.GetByUuidAsync("UUID-1");

        Assert.NotNull(found);
        Assert.Equal("first", found!.Name);
    }

    [Fact]
    public async Task InMemory_store_returns_all_and_hides_disabled()
    {
        var store = new InMemoryWebhookConfigStore(new[]
        {
            new WebhookDefinition { Uuid = "a", Enabled = true },
            new WebhookDefinition { Uuid = "b", Enabled = false }
        });

        Assert.Equal(2, (await store.GetAllAsync()).Count);
        Assert.NotNull(await store.GetByUuidAsync("a"));
        Assert.Null(await store.GetByUuidAsync("b"));
    }
}
