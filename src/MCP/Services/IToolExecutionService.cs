using McpServer.Models;
using PI.Shared.Models;

namespace McpServer.Services;

public interface IToolExecutionService
{
    Task<ToolCallResult> ExecuteToolAsync(IEntityContext? context, string toolName, Dictionary<string, object>? arguments);
}
