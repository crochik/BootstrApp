using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Webhook.Publisher.Configuration;
using Webhook.Publisher.Delivery;
using Webhook.Publisher.Messaging;
using Webhook.Publisher.Publishing;
using Webhook.Publisher.Storage;
using Webhook.Publisher.Subscriptions;

namespace Webhook.Publisher.DependencyInjection;

/// <summary>
/// Registers the outbound webhook publisher, delivery worker and subscription store.
/// Mirrors the inbound service's <c>WebhookServiceCollectionExtensions</c>.
/// </summary>
public static class WebhookPublisherServiceCollectionExtensions
{
    /// <summary>
    /// Registers the publish side: MongoDB store, RabbitMQ connection/topology and
    /// <see cref="IWebhookPublisher"/>. A JSON-file subscription store is registered as
    /// the default; call <see cref="AddWebhookSubscriptionStore{TStore}"/> to override it.
    /// </summary>
    public static IServiceCollection AddWebhookPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions();
        services.Configure<WebhookPublisherOptions>(configuration.GetSection(WebhookPublisherOptions.SectionName));
        services.Configure<WebhookSubscriptionOptions>(configuration.GetSection(WebhookSubscriptionOptions.SectionName));

        // MongoDB — the source of truth for payloads and delivery status.
        services.TryAddSingleton<IMongoClient>(sp =>
            new MongoClient(sp.GetRequiredService<IOptions<WebhookPublisherOptions>>().Value.Mongo.ConnectionString));
        services.TryAddSingleton<IWebhookEventStore, MongoWebhookEventStore>();

        // Broker object names are derived from the configured prefix.
        services.TryAddSingleton(sp =>
            new WebhookTopologyNames(sp.GetRequiredService<IOptions<WebhookPublisherOptions>>().Value.RabbitMq.ExchangePrefix));

        services.TryAddSingleton<IWebhookConnectionManager, RabbitMqConnectionManager>();
        services.TryAddSingleton<IWebhookTopologyInitializer, RabbitMqTopologyInitializer>();
        services.AddHostedService<WebhookTopologyHostedService>();

        // Default subscription store; overridable via AddWebhookSubscriptionStore<T>().
        services.TryAddSingleton<IWebhookSubscriptionStore, JsonFileWebhookSubscriptionStore>();

        services.TryAddSingleton<IWebhookPublisher, RabbitMqWebhookPublisher>();

        return services;
    }

    /// <summary>
    /// Registers the publish side plus the delivery worker, HTTP delivery client,
    /// signer and outbox reconciler.
    /// </summary>
    public static IServiceCollection AddWebhookDeliveryWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWebhookPublisher(configuration);

        services.TryAddSingleton<IWebhookSigner, HmacWebhookSigner>();

        // Typed client so IHttpClientFactory manages pooled handlers (no socket exhaustion).
        services.AddHttpClient<HttpWebhookDeliveryClient>();
        services.TryAddTransient<IWebhookDeliveryClient>(sp => sp.GetRequiredService<HttpWebhookDeliveryClient>());

        services.AddHostedService<WebhookDeliveryWorker>();
        services.AddHostedService<WebhookOutboxReconciler>();

        return services;
    }

    /// <summary>Overrides the subscription store with a custom implementation.</summary>
    public static IServiceCollection AddWebhookSubscriptionStore<TStore>(this IServiceCollection services)
        where TStore : class, IWebhookSubscriptionStore
    {
        services.RemoveAll<IWebhookSubscriptionStore>();
        services.AddSingleton<IWebhookSubscriptionStore, TStore>();
        return services;
    }
}
