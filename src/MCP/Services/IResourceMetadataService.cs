using McpServer.Models;

namespace McpServer.Services;

public interface IResourceMetadataService
{
    Task<IReadOnlyList<ResourceMetadata>> GetAvailableResourcesAsync();
    Task<ResourceMetadata?> GetResourceMetadataAsync(string uri);
}
