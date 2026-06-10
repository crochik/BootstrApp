using McpServer.Models;
using PI.Shared.Models;

namespace McpServer.Services;

public interface IMcpProtocolHandler
{
    Task<McpResponse> HandleRequestAsync(
        IEntityContext? context,
        McpRequest request);
}
