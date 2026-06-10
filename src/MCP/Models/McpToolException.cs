namespace McpServer.Models;

/// <summary>
/// Thrown by tool methods to signal a recoverable error that should be surfaced
/// to the LLM via the MCP <c>isError: true</c> result. The framework catches this
/// and converts it into a <see cref="ToolCallResult"/>; other exception types are
/// treated as bugs (logged in full, returned as a sanitized generic message).
/// </summary>
public sealed class McpToolException : Exception
{
    public List<ToolContent> Content { get; }
    public object? StructuredContent { get; }

    public McpToolException(string message) : base(message)
    {
        Content = [new ToolContent { Type = "text", Text = message }];
    }

    public McpToolException(string message, object? structuredContent) : base(message)
    {
        Content = [new ToolContent { Type = "text", Text = message }];
        StructuredContent = structuredContent;
    }

    public McpToolException(IEnumerable<ToolContent> content, object? structuredContent = null)
        : base(content.FirstOrDefault()?.Text ?? "Tool error")
    {
        Content = content.ToList();
        StructuredContent = structuredContent;
    }
}
