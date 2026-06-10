namespace McpServer.Tools.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>
    /// Tool name exposed via MCP protocol. Defaults to snake_case of the method name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Human-readable tool description for LLM consumption.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether a valid authenticated IEntityContext is required to invoke this tool.
    /// Defaults to true (secure by default).
    /// </summary>
    public bool RequiresAuthentication { get; set; } = true;

    /// <summary>
    /// Example natural-language prompts that demonstrate how a caller might invoke this tool.
    /// </summary>
    public string[]? ExamplePrompts { get; set; }

    /// <summary>
    /// When true, the tool is hidden from the default tools/list response and is only
    /// discoverable via the built-in tool_search meta-tool. The tool remains callable
    /// by name through tools/call. Use this to keep large or rarely-used tools out of
    /// the LLM's default context window.
    /// </summary>
    public bool Deferred { get; set; } = false;

    /// <summary>
    /// When true, the tool's return type is introspected at startup to publish an
    /// <c>outputSchema</c> in <c>tools/list</c>, and tool results carry the value as
    /// <c>structuredContent</c> in <c>tools/call</c>. Methods returning <see cref="string"/>
    /// or <see cref="Models.ToolCallResult"/> cannot opt in.
    /// </summary>
    public bool StructuredOutput { get; set; } = false;
}
