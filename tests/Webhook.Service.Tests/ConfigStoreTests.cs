using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Webhook.Service.Config;
using Webhook.Service.Configuration;
using Xunit;

namespace Webhook.Service.Tests;

public class ConfigStoreTests
{
    private static IWebhookConfigStore BuildJsonStore()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Webhooks:Definitions:0:Uuid"] = "uuid-1",
                ["Webhooks:Definitions:0:Name"] = "first",
                ["Webhooks:Definitions:0:Enabled"] = "true",
                ["Webhooks:Definitions:1:Uuid"] = "uuid-2",
                ["Webhooks:Definitions:1:Name"] = "disabled",
                ["Webhooks:Definitions:1:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<WebhookOptions>(config.GetSection(WebhookOptions.SectionName));
        var provider = services.BuildServiceProvider();

        return new JsonFileWebhookConfigStore(provider.GetRequiredService<IOptionsMonitor<WebhookOptions>>());
    }

    [Fact]
    public async Task Json_store_resolves_enabled_definition_case_insensitively()
    {
        var store = BuildJsonStore();

        var found = await store.GetByUuidAsync("UUID-1");

        Assert.NotNull(found);
        Assert.Equal("first", found!.Name);
    }

    [Fact]
    public async Task Json_store_hides_disabled_definition()
    {
        var store = BuildJsonStore();

        Assert.Null(await store.GetByUuidAsync("uuid-2"));
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
