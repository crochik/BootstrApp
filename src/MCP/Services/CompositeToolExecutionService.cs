using McpServer.Models;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace McpServer.Services;

/// <summary>
/// Routes tool execution to the first IToolSource that claims the tool.
/// Each source returns null from TryExecuteAsync if it does not own the named tool.
/// </summary>
public sealed class CompositeToolExecutionService : IToolExecutionService
{
    private readonly IEnumerable<IToolSource> _sources;
    private readonly ILogger<CompositeToolExecutionService> _logger;

    public CompositeToolExecutionService(
        IEnumerable<IToolSource> sources,
        ILogger<CompositeToolExecutionService> logger)
    {
        _sources = sources;
        _logger = logger;
    }

    public async Task<ToolCallResult> ExecuteToolAsync(
        IEntityContext? context,
        string toolName,
        Dictionary<string, object>? arguments)
    {
        foreach (var source in _sources)
        {
            var result = await source.TryExecuteAsync(context, toolName, arguments);
            if (result != null)
                return result;
        }

        _logger.LogWarning("No source handled tool '{ToolName}'", toolName);
        return ToolCallResult.Error($"Tool '{toolName}' not found");
    }
}
