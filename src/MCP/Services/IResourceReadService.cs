using McpServer.Models;
using PI.Shared.Models;

namespace McpServer.Services;

public interface IResourceReadService
{
    Task<ResourceReadResult> ReadResourceAsync(IEntityContext? context, string uri);
}
