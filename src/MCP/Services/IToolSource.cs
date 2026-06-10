using McpServer.Models;
using PI.Shared.Models;

namespace McpServer.Services;

/// <summary>
/// Represents a single pluggable source of MCP tools.
/// Register multiple implementations via IServiceCollection to combine sources.
/// </summary>
/// <remarks>
/// <c>IToolMetadataService</c> and <c>IToolExecutionService</c> are the stable
/// single-entry-points used by the rest of the system. <c>IToolSource</c> is the
/// multi-registration extension point — each source owns a set of tools and knows
/// how to execute them.
/// </remarks>
public interface IToolSource
{
    /// <summary>Returns all tools this source provides.</summary>
    Task<IReadOnlyList<ToolMetadata>> GetToolsAsync();

    /// <summary>
    /// Attempts to execute the named tool.
    /// Returns <c>null</c> if this source does not own the tool,
    /// allowing the composite to try the next registered source.
    /// </summary>
    Task<ToolCallResult?> TryExecuteAsync(
        IEntityContext? context,
        string toolName,
        Dictionary<string, object>? arguments);

    /// <summary>
    /// Returns up to <paramref name="maxResults"/> tools from this source ranked by
    /// relevance to a space-separated keyword <paramref name="query"/>. The
    /// default implementation performs an in-memory substring scoring pass over
    /// <see cref="GetToolsAsync"/>; override to delegate to a backing store
    /// (database FTS, trigram index, embeddings) when this source can rank more
    /// efficiently than scanning every tool.
    /// </summary>
    /// <remarks>
    /// Implementations should ignore <c>select:</c>-prefixed queries — the
    /// meta-tool handles exact-name lookup against <see cref="GetToolsAsync"/>
    /// directly. Returned results should already be ranked best-first; the
    /// composite merges across sources by per-source rank.
    /// </remarks>
    async Task<IReadOnlyList<ToolMetadata>> SearchAsync(string query, int maxResults)
    {
        var tools = await GetToolsAsync();
        return DefaultToolSearch.Score(tools, query, maxResults);
    }
}

/// <summary>
/// In-memory substring-and-weight scorer used as the fallback for
/// <see cref="IToolSource.SearchAsync"/> when a source doesn't override it.
/// Exposed so sources that need only minor tweaks can delegate to it.
/// </summary>
public static class DefaultToolSearch
{
    public static IReadOnlyList<ToolMetadata> Score(
        IReadOnlyList<ToolMetadata> tools, string query, int maxResults)
    {
        var terms = query
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToArray();

        if (terms.Length == 0 || maxResults < 1) return Array.Empty<ToolMetadata>();

        return tools
            .Select(t => new { Tool = t, Score = ScoreTool(t, terms) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Tool.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(x => x.Tool)
            .ToList();
    }

    private static int ScoreTool(ToolMetadata tool, string[] terms)
    {
        int score = 0;
        var name = tool.Name.ToLowerInvariant();
        var description = (tool.Description ?? string.Empty).ToLowerInvariant();
        var examples = tool.ExamplePrompts == null
            ? string.Empty
            : string.Join(' ', tool.ExamplePrompts).ToLowerInvariant();

        foreach (var term in terms)
        {
            if (name.Contains(term)) score += 3;
            if (description.Contains(term)) score += 1;
            if (examples.Contains(term)) score += 1;
        }

        return score;
    }
}
