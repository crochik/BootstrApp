using Microsoft.Extensions.DependencyInjection;
using Webhook.Service.Config;
using Webhook.Service.Engine;
using Webhook.Service.Formats;
using Webhook.Service.Handlers;
using Webhook.Service.Validation;

namespace Webhook.Service.DependencyInjection;

/// <summary>
/// Registers every component of the webhook engine. Consumers can add their own
/// handlers/validators/parsers afterwards by calling the matching
/// <c>Add*</c> helpers (they are additive thanks to <c>IEnumerable</c> injection).
/// </summary>
public static class WebhookServiceCollectionExtensions
{
    public static IServiceCollection AddWebhookEngine(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WebhookOptions>(configuration.GetSection(WebhookOptions.SectionName));

        // Pluggable config store (JSON-file backed, hot-reloads with configuration).
        services.AddSingleton<IWebhookConfigStore, JsonFileWebhookConfigStore>();

        // Validators — one per auth type. TokenHeaderValidator serves bearer + apikey.
        services.AddSingleton<IWebhookValidator, HmacSignatureValidator>();
        services.AddSingleton<IWebhookValidator>(_ => new TokenHeaderValidator("bearer"));
        services.AddSingleton<IWebhookValidator>(_ => new TokenHeaderValidator("apikey"));
        services.AddSingleton<IWebhookValidator, BasicAuthValidator>();
        services.AddSingleton<IWebhookValidator, IpAllowlistValidator>();
        services.AddSingleton<IWebhookValidator, TwilioSignatureValidator>();
        services.AddSingleton<IWebhookValidator, EcdsaSignatureValidator>();
        services.AddSingleton<IWebhookValidator>(_ => new SignedTimestampValidator("stripe"));
        services.AddSingleton<IWebhookValidator>(_ => new SignedTimestampValidator("docuseal"));
        services.AddSingleton<IWebhookValidator>(_ => new SignedTimestampValidator("openphone"));
        services.AddSingleton<IWebhookValidator, ClientCertValidator>();
        services.AddSingleton<IWebhookValidator, BodyFieldValidator>();
        services.AddSingleton<WebhookValidationPipeline>();

        // Payload parsers.
        services.AddSingleton<IPayloadParser, JsonPayloadParser>();
        services.AddSingleton<IPayloadParser, FormUrlEncodedPayloadParser>();
        services.AddSingleton<IPayloadParser, XmlPayloadParser>();
        services.AddSingleton<IPayloadParser, RawPayloadParser>();
        services.AddSingleton<PayloadParserRegistry>();

        // Handlers + dispatch.
        services.AddSingleton<IWebhookHandler, LoggingWebhookHandler>();
        services.AddSingleton<IWebhookHandlerRegistry, WebhookHandlerRegistry>();

        services.AddSingleton<WebhookProcessor>();

        return services;
    }

    /// <summary>Registers an additional <see cref="IWebhookHandler"/> implementation.</summary>
    public static IServiceCollection AddWebhookHandler<THandler>(this IServiceCollection services)
        where THandler : class, IWebhookHandler
    {
        services.AddSingleton<IWebhookHandler, THandler>();
        return services;
    }
}
