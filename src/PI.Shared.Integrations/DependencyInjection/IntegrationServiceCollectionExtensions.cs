using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PI.Shared.Integrations.Catalog;
using PI.Shared.Integrations.Delivery;
using PI.Shared.Integrations.Subscriptions;

namespace PI.Shared.Integrations.DependencyInjection;

/// <summary>
/// Registers the shared pieces every outbound integration uses: the
/// <c>ObjectType</c>-driven catalog, sample generation, the integration's Mongo-backed
/// subscription store, and the durable signed-delivery pipeline (event/delivery store,
/// signer, HTTP client, publisher).
/// <para>
/// The lifetime services — <see cref="WebhookEventListenerService"/>,
/// <see cref="WebhookDeliveryWorkerService"/> and <see cref="WebhookOutboxReconcilerService"/> — are
/// registered by the host service via <c>AddLifetimeService</c> so they participate in
/// the standard start/stop lifecycle.
/// </para>
/// </summary>
public static class IntegrationServiceCollectionExtensions
{
    /// <param name="configuration">Root configuration; the <c>WebhookDelivery</c> section tunes delivery/retry.</param>
    /// <typeparam name="TSubscription">The integration's concrete subscription type (carries its <c>[BsonCollection]</c>).</typeparam>
    public static IServiceCollection AddIntegrationServices<TSubscription>(this IServiceCollection services, IConfiguration configuration)
        where TSubscription : IntegrationSubscription, new()
    {
        services.Configure<DeliveryOptions>(configuration.GetSection(DeliveryOptions.SectionName));

        // Catalog + samples, discovered from the account's real ObjectType definitions.
        services.AddSingleton<IObjectCatalog, ObjectTypeCatalog>();
        services.AddSingleton<ISampleFactory, ObjectTypeSampleFactory>();

        // The integration's own subscription collection.
        services.AddSingleton<ISubscriptionStore, MongoSubscriptionStore<TSubscription>>();

        // Durable delivery pipeline.
        services.AddSingleton<IWebhookStore, MongoWebhookStore>();
        services.AddSingleton<IWebhookSigner, HmacWebhookSigner>();
        services.AddSingleton<IEventPublisher, WebhookEventPublisher>();

        // Typed client so IHttpClientFactory manages pooled handlers (no socket exhaustion).
        services.AddHttpClient<HttpWebhookDeliveryClient>();
        services.AddTransient<IWebhookDeliveryClient>(sp => sp.GetRequiredService<HttpWebhookDeliveryClient>());

        return services;
    }
}
