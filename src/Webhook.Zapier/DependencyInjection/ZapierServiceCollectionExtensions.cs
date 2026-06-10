using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhook.Integrations.Core.DependencyInjection;

namespace Webhook.Zapier.DependencyInjection;

/// <summary>
/// Wires the Zapier integration: the shared integration core (discovery, subscription
/// bridge, durable delivery) bound to the <c>Zapier</c> configuration section and the
/// <c>/zapier</c> route prefix. The controllers in this project provide the Zapier API.
/// </summary>
public static class ZapierServiceCollectionExtensions
{
    public static IServiceCollection AddZapierIntegration(this IServiceCollection services, IConfiguration configuration) =>
        services.AddIntegrationCore(configuration, sectionName: "Zapier", routePrefix: "/zapier");

    /// <summary>Inserts the API-key gate in front of the <c>/zapier/*</c> endpoints.</summary>
    public static IApplicationBuilder UseZapierApiKeyAuth(this IApplicationBuilder app) =>
        app.UseIntegrationApiKeyAuth();
}
