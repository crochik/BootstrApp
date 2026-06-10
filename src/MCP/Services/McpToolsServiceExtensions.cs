using McpServer.Tools;
using McpServer.Tools.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace McpServer.Services;

public static class McpToolsServiceExtensions
{
    /// <summary>
    /// Registers the MCP tool system with composite metadata and execution services.
    /// Use the builder to add tool sources.
    /// </summary>
    /// <example>
    /// services.AddMcpTools(tools =>
    /// {
    ///     tools.AddToolType&lt;UserProfileTools&gt;();      // attribute-based
    ///     tools.AddToolSource&lt;DatabaseToolSource&gt;();   // custom source
    /// });
    /// </example>
    public static IServiceCollection AddMcpTools(
        this IServiceCollection services,
        Action<McpToolsBuilder> configure)
    {
        var registry = new ToolRegistry();
        var builder = new McpToolsBuilder(registry, services);
        configure(builder);

        services.AddSingleton(registry);

        // AttributeToolSource registered only when AddToolType<T>() was called
        if (builder.HasAttributeTools)
            services.AddSingleton<IToolSource, AttributeToolSource>();

        // Built-in tool_search meta-tool. Registered last so it never shadows
        // names from user-supplied sources, and so it sees every other source
        // when resolving IEnumerable<IToolSource>. Opt-out via DisableToolSearch().
        if (!builder.IsToolSearchDisabled)
            services.AddSingleton<IToolSource, ToolSearchSource>();

        // Composites are the canonical IToolMetadataService / IToolExecutionService
        services.AddSingleton<IToolMetadataService, CompositeToolMetadataService>();
        services.AddSingleton<IToolExecutionService, CompositeToolExecutionService>();
        return services;
    }
}

public sealed class McpToolsBuilder
{
    private readonly ToolRegistry _registry;
    private readonly IServiceCollection _services;

    internal bool HasAttributeTools { get; private set; }
    internal bool IsToolSearchDisabled { get; private set; }

    internal McpToolsBuilder(ToolRegistry registry, IServiceCollection services)
    {
        _registry = registry;
        _services = services;
    }

    /// <summary>
    /// Registers a tool class. All public methods decorated with [McpTool] are discovered
    /// and exposed as MCP tools. The class is registered as Transient in DI,
    /// supporting constructor injection of any registered services.
    /// </summary>
    public McpToolsBuilder AddToolType<T>() where T : class
    {
        _registry.Register(typeof(T));
        _services.AddTransient<T>();
        HasAttributeTools = true;
        return this;
    }

    /// <summary>
    /// Registers a custom IToolSource implementation.
    /// Use this to add tool sources beyond the attribute-based system
    /// (e.g., database-backed tools, plugin systems, test stubs).
    /// </summary>
    public McpToolsBuilder AddToolSource<T>() where T : class, IToolSource
    {
        _services.AddSingleton<IToolSource, T>();
        return this;
    }

    /// <summary>
    /// Registers the built-in <c>authenticate</c> / <c>complete_authentication</c>
    /// tool pair, matching the Anthropic-connector convention. Both tools are
    /// callable without auth and return guidance for running MCP OAuth (RFC 9728);
    /// they do not mint tokens — the server stays stateless JWT-bearer.
    /// Requires <c>IHttpContextAccessor</c> in DI; this helper registers it.
    /// </summary>
    public McpToolsBuilder AddAuthenticationTools()
    {
        _services.AddHttpContextAccessor();
        return AddToolType<AuthenticationTools>();
    }

    /// <summary>
    /// Disables the built-in <c>tool_search</c> meta-tool. By default the search
    /// source is registered automatically so deferred tools remain discoverable.
    /// Call this if your consumer ships its own discovery tool or wants no
    /// meta-tool at all.
    /// </summary>
    public McpToolsBuilder DisableToolSearch()
    {
        IsToolSearchDisabled = true;
        return this;
    }
}
