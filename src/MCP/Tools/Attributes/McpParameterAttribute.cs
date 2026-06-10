namespace McpServer.Tools.Attributes;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class McpParameterAttribute : Attribute
{
    /// <summary>
    /// Human-readable description of this parameter for LLM consumption.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this parameter is required. Defaults to true.
    /// Parameters with C# default values are automatically treated as optional.
    /// </summary>
    public bool Required { get; set; } = false;
}
