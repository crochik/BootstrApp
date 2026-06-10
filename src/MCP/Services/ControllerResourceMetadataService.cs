using McpServer.Models;
using McpServer.Resources.Registry;

namespace McpServer.Services;

/// <summary>
/// Singleton wrapper around <see cref="ResourceRegistry"/>. Surfaces the cached
/// list of MCP resources discovered from controller actions decorated with
/// <see cref="Resources.Attributes.McpResourceAttribute"/>.
/// </summary>
public sealed class ControllerResourceMetadataService : IResourceMetadataService
{
    private readonly ResourceRegistry _registry;

    public ControllerResourceMetadataService(ResourceRegistry registry)
    {
        _registry = registry;
    }

    public Task<IReadOnlyList<ResourceMetadata>> GetAvailableResourcesAsync()
    {
        return Task.FromResult(_registry.Metadata);
    }

    public Task<ResourceMetadata?> GetResourceMetadataAsync(string uri)
    {
        _registry.Resources.TryGetValue(uri, out var registration);
        return Task.FromResult(registration?.Metadata);
    }
}
