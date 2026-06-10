namespace McpServer.Resources.Attributes;

/// <summary>
/// Marks a controller GET action as an MCP resource, discoverable via
/// <c>resources/list</c> and readable via <c>resources/read</c>.
///
/// Important: this attribute does NOT change the action's REST accessibility.
/// The decorated action is still routed normally (e.g. <c>GET /api/config</c>)
/// and REST auth is governed only by <c>[Authorize]</c> on the controller/action.
/// The MCP path uses <see cref="RequiresAuthentication"/> as a separate, independent
/// gate. To protect the data on both paths, apply both attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpResourceAttribute : Attribute
{
    /// <summary>
    /// MCP resource URI. If null, the action's route template is used (e.g. "api/config").
    /// Parameterized routes (containing "{...}") are rejected at startup — resource
    /// templates are not yet supported.
    /// </summary>
    public string? Uri { get; set; }

    /// <summary>
    /// Display name. Defaults to the action method name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Human-readable description for LLM consumption.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the resource content (e.g. "application/json", "text/plain").
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Whether the MCP path requires an authenticated <see cref="PI.Shared.Models.IEntityContext"/>.
    /// Defaults to true (secure by default). Independent of <c>[Authorize]</c> on the
    /// controller — apply <c>[Authorize]</c> as well to protect the REST path.
    /// </summary>
    public bool RequiresAuthentication { get; set; } = true;
}
