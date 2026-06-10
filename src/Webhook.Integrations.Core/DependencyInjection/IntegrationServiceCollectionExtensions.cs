using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhook.Integrations.Core.Auth;
using Webhook.Integrations.Core.Catalog;
using Webhook.Integrations.Core.Configuration;
using Webhook.Integrations.Core.Delivery;
using Webhook.Integrations.Core.Mock;
using Webhook.Integrations.Core.Subscriptions;
using Webhook.Publisher.DependencyInjection;
using IPublisherSubscriptionStore = Webhook.Publisher.Subscriptions.IWebhookSubscriptionStore;

namespace Webhook.Integrations.Core.DependencyInjection;

/// <summary>
/// Registers the pieces every integration shares: runtime catalog discovery, sample
/// generation, the subscription bridge, the API-key gate and durable delivery through
/// <c>Webhook.Publisher</c>. Each integration (Zapier, n8n, …) adds only its own
/// controllers on top.
/// </summary>
public static class IntegrationServiceCollectionExtensions
{
    /// <summary>
    /// Wires the shared integration core.
    /// </summary>
    /// <param name="configuration">Root configuration (the <c>WebhookPublisher</c> section drives delivery).</param>
    /// <param name="sectionName">Integration config section bound to <see cref="IntegrationOptions"/> (e.g. <c>Zapier</c>).</param>
    /// <param name="routePrefix">Route prefix the API-key gate protects (e.g. <c>/zapier</c>).</param>
    /// <param name="catalogAssemblies">Assemblies scanned for decorated objects; defaults to the core assembly (the mock domain).</param>
    public static IServiceCollection AddIntegrationCore(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        string routePrefix,
        params Assembly[] catalogAssemblies)
    {
        services.Configure<IntegrationOptions>(configuration.GetSection(sectionName));
        services.PostConfigure<IntegrationOptions>(o => o.RoutePrefix = routePrefix);

        var assemblies = catalogAssemblies.Length > 0
            ? catalogAssemblies
            : new[] { typeof(IntegrationServiceCollectionExtensions).Assembly };

        // Catalog + samples are immutable; register the discovered instance as a singleton.
        services.AddSingleton<IEventCatalog>(_ => new ReflectionEventCatalog(assemblies));
        services.AddSingleton<ISampleFactory, ReflectionSampleFactory>();

        // One bridge store, exposed to both the controllers (add/remove) and the
        // publisher (read). Registering it as IWebhookSubscriptionStore *before*
        // AddWebhookDeliveryWorker means the pipeline's TryAdd default (the JSON-file
        // store) is skipped and ours is used instead.
        services.AddSingleton<IntegrationSubscriptionStore>();
        services.AddSingleton<ISubscriptionStore>(sp => sp.GetRequiredService<IntegrationSubscriptionStore>());
        services.AddSingleton<IPublisherSubscriptionStore>(sp => sp.GetRequiredService<IntegrationSubscriptionStore>());

        // Durable outbound delivery: publisher + delivery worker + retries + signing.
        services.AddWebhookDeliveryWorker(configuration);

        services.AddSingleton<IEventPublisher, PublisherEventPublisher>();
        services.AddTransient<MockEventEmitter>();

        return services;
    }

    /// <summary>Inserts the API-key gate in front of the integration's protected routes.</summary>
    public static IApplicationBuilder UseIntegrationApiKeyAuth(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiKeyMiddleware>();
}
