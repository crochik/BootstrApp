using McpServer.Resources.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace McpServer.Services;

public static class McpResourcesServiceExtensions
{
    /// <summary>
    /// Registers the MCP resource system. Discovery is automatic — every controller
    /// action decorated with <see cref="Resources.Attributes.McpResourceAttribute"/>
    /// is exposed via <c>resources/list</c> and <c>resources/read</c>.
    ///
    /// Call after <c>AddControllers()</c> so controller action descriptors are
    /// available when the registry first resolves.
    /// </summary>
    /// <example>
    /// services.AddControllers();
    /// services.AddMcpResources();
    /// </example>
    public static IServiceCollection AddMcpResources(this IServiceCollection services)
    {
        services.AddSingleton<ResourceRegistry>();
        services.AddSingleton<IResourceMetadataService, ControllerResourceMetadataService>();
        services.AddSingleton<IResourceReadService, ControllerResourceReadService>();
        return services;
    }
}
