using McpServer.Models;
using Microsoft.Extensions.Logging;

namespace McpServer.Services;

/// <summary>
/// Aggregates tool metadata from all registered IToolSource instances.
/// Tool names are unique across sources; first-registration-wins on duplicates.
/// </summary>
public sealed class CompositeToolMetadataService : IToolMetadataService
{
    private readonly IEnumerable<IToolSource> _sources;
    private readonly ILogger<CompositeToolMetadataService> _logger;

    public CompositeToolMetadataService(
        IEnumerable<IToolSource> sources,
        ILogger<CompositeToolMetadataService> logger)
    {
        _sources = sources;
        _logger = logger;
    }

    public async Task<List<ToolMetadata>> GetAvailableToolsAsync()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ToolMetadata>();
        int deferredCount = 0;

        foreach (var source in _sources)
        {
            foreach (var tool in await source.GetToolsAsync())
            {
                if (!seen.Add(tool.Name))
                {
                    _logger.LogWarning(
                        "Duplicate tool name '{ToolName}' from {Source} — skipped (first-registration-wins)",
                        tool.Name, source.GetType().Name);
                    continue;
                }

                if (tool.Deferred)
                {
                    deferredCount++;
                    continue;
                }

                result.Add(tool);
            }
        }

        _logger.LogInformation(
            "Returning {Count} tools from {SourceCount} source(s); {Deferred} deferred tool(s) hidden",
            result.Count, _sources.Count(), deferredCount);
        return result;
    }

    public async Task<ToolMetadata?> GetToolMetadataAsync(string toolName)
    {
        foreach (var source in _sources)
        {
            var tools = await source.GetToolsAsync();
            var match = tools.FirstOrDefault(t =>
                string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }
        return null;
    }
}
