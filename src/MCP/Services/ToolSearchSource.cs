using System.Text.Json;
using McpServer.Models;
using Microsoft.Extensions.DependencyInjection;
using PI.Shared.Models;

namespace McpServer.Services;

/// <summary>
/// Built-in IToolSource that exposes a single non-deferred tool, <c>tool_search</c>,
/// which lets the model discover deferred tools' metadata on demand. Tools surfaced
/// by the search remain callable through the normal tools/call path because
/// CompositeToolExecutionService never filters by Deferred.
/// </summary>
public sealed class ToolSearchSource : IToolSource
{
    public const string ToolName = "tool_search";

    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    private readonly IServiceProvider _serviceProvider;
    private readonly Lazy<IReadOnlyList<IToolSource>> _otherSources;
    private readonly IReadOnlyList<ToolMetadata> _selfTools;

    public ToolSearchSource(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _otherSources = new Lazy<IReadOnlyList<IToolSource>>(ResolveOtherSources);
        _selfTools = new[] { BuildSearchToolMetadata() };
    }

    public Task<IReadOnlyList<ToolMetadata>> GetToolsAsync() =>
        Task.FromResult(_selfTools);

    public async Task<ToolCallResult?> TryExecuteAsync(
        IEntityContext? context,
        string toolName,
        Dictionary<string, object>? arguments)
    {
        if (!string.Equals(toolName, ToolName, StringComparison.OrdinalIgnoreCase))
            return null;

        var query = ReadString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query))
            return Error("Required parameter 'query' is missing.");

        int maxResults = ReadInt(arguments, "max_results", defaultValue: 5);
        if (maxResults < 1) maxResults = 1;

        IEnumerable<ToolMetadata> matches;
        if (query.StartsWith("select:", StringComparison.OrdinalIgnoreCase))
        {
            var allTools = await CollectAllToolsAsync();
            matches = ExactSelect(allTools, query["select:".Length..]);
        }
        else
        {
            matches = await KeywordSearchAcrossSourcesAsync(query, maxResults);
        }

        var payload = matches.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            inputSchema = t.InputSchema,
            examplePrompts = t.ExamplePrompts
        });

        return new ToolCallResult
        {
            Content =
            {
                new ToolContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(payload, PrettyJson)
                }
            }
        };
    }

    private async Task<List<ToolMetadata>> CollectAllToolsAsync()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = new List<ToolMetadata>();

        foreach (var source in _otherSources.Value)
        {
            foreach (var tool in await source.GetToolsAsync())
            {
                if (seen.Add(tool.Name))
                    all.Add(tool);
            }
        }

        return all;
    }

    private IReadOnlyList<IToolSource> ResolveOtherSources() =>
        _serviceProvider.GetServices<IToolSource>()
            .Where(s => !ReferenceEquals(s, this))
            .ToList();

    private static IEnumerable<ToolMetadata> ExactSelect(
        IEnumerable<ToolMetadata> tools, string commaSeparated)
    {
        var requested = commaSeparated
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return tools.Where(t => requested.Contains(t.Name));
    }

    private async Task<List<ToolMetadata>> KeywordSearchAcrossSourcesAsync(
        string query, int maxResults)
    {
        var perSource = new List<IReadOnlyList<ToolMetadata>>(_otherSources.Value.Count);
        foreach (var source in _otherSources.Value)
        {
            var hits = await source.SearchAsync(query, maxResults);
            if (hits.Count > 0) perSource.Add(hits);
        }

        return InterleaveAndDedupe(perSource, maxResults);
    }

    /// <summary>
    /// Round-robin merge across sources: take rank-0 from every source, then rank-1, etc.,
    /// deduping by tool name (first-seen wins) and stopping at <paramref name="maxResults"/>.
    /// Each source ranks internally on its own scale; this avoids needing a normalized
    /// cross-source score while still giving every source a chance to surface its top hit.
    /// </summary>
    private static List<ToolMetadata> InterleaveAndDedupe(
        List<IReadOnlyList<ToolMetadata>> perSource, int maxResults)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ToolMetadata>();

        int rank = 0;
        bool any;
        do
        {
            any = false;
            foreach (var list in perSource)
            {
                if (rank >= list.Count) continue;
                any = true;
                var tool = list[rank];
                if (!seen.Add(tool.Name)) continue;
                result.Add(tool);
                if (result.Count >= maxResults) return result;
            }
            rank++;
        } while (any);

        return result;
    }

    private static ToolMetadata BuildSearchToolMetadata() => new()
    {
        Name = ToolName,
        Description =
            "Search the server's full tool catalog (including deferred tools hidden " +
            "from tools/list) and return their schemas. Use 'select:name1,name2' for " +
            "exact-name lookup, or space-separated keywords for fuzzy match against " +
            "tool names, descriptions, and example prompts. Returned tools can be " +
            "invoked via the normal tools/call flow.",
        RequiresAuthentication = false,
        Deferred = false,
        InputSchema = new ToolInputSchema
        {
            Properties = new Dictionary<string, PropertySchema>
            {
                ["query"] = new()
                {
                    Type = "string",
                    Description =
                        "Either 'select:name1,name2,...' for exact-name lookup, or " +
                        "space-separated keywords for fuzzy match."
                },
                ["max_results"] = new()
                {
                    Type = "number",
                    Description = "Maximum number of keyword-search results to return (default 5). Ignored for select:."
                }
            },
            Required = new List<string> { "query" }
        }
    };

    private static string? ReadString(Dictionary<string, object>? args, string key)
    {
        if (args == null || !args.TryGetValue(key, out var raw) || raw == null) return null;
        if (raw is string s) return s;
        if (raw is JsonElement el && el.ValueKind == JsonValueKind.String) return el.GetString();
        return raw.ToString();
    }

    private static int ReadInt(Dictionary<string, object>? args, string key, int defaultValue)
    {
        if (args == null || !args.TryGetValue(key, out var raw) || raw == null) return defaultValue;
        if (raw is int i) return i;
        if (raw is long l) return (int)l;
        if (raw is double d) return (int)d;
        if (raw is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var ns)) return ns;
        }
        if (raw is string str && int.TryParse(str, out var parsed)) return parsed;
        return defaultValue;
    }

    private static ToolCallResult Error(string message) => new()
    {
        IsError = true,
        Content = { new ToolContent { Type = "text", Text = message } }
    };
}
