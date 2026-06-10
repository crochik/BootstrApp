using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhook.Integrations.Core.DependencyInjection;

namespace Webhook.N8n.DependencyInjection;

/// <summary>
/// Wires the n8n integration: the shared integration core (discovery, subscription
/// bridge, durable delivery) bound to the <c>N8n</c> configuration section and the
/// <c>/n8n</c> route prefix. The controllers in this project provide the n8n API.
/// </summary>
public static class N8nServiceCollectionExtensions
{
    public static IServiceCollection AddN8nIntegration(this IServiceCollection services, IConfiguration configuration) =>
        services.AddIntegrationCore(configuration, sectionName: "N8n", routePrefix: "/n8n");

    /// <summary>Inserts the API-key gate in front of the <c>/n8n/*</c> endpoints.</summary>
    public static IApplicationBuilder UseN8nApiKeyAuth(this IApplicationBuilder app) =>
        app.UseIntegrationApiKeyAuth();
}
