using PI.Shared.Models;
using ScriptInterpreter.Execution;
using ScriptInterpreter.Plugins;
using ScriptInterpreter.Registry;

namespace MCP.Services;

/// <summary>
/// Built-in plugin exposing the running session's identity (the request's <see cref="IEntityContext"/>)
/// under the <c>system.session</c> namespace — e.g. <c>system.session.getUserId()</c>.
///
/// Implemented as a reflection plugin over an internal <see cref="SessionService"/> (mirrors
/// <see cref="ScriptInterpreter.Plugins.SystemDatePlugin"/>): each method declares a hidden
/// <see cref="IExecutionContext"/> parameter that the runtime injects at dispatch time and resolves
/// the session via <c>ctx.GetRequiredService&lt;IEntityContext&gt;()</c>. The host must register the
/// session before running a script:
/// <code>
/// var ctx = new ExecutionContext();
/// ctx.AddService&lt;IEntityContext&gt;(context);
/// var env = ScriptEnvironment.FromPlugins(new SystemSessionScriptPlugin(), /* other plugins */);
/// await env.ExecuteAsync(source, ctx);
/// </code>
/// </summary>
public sealed class SystemSessionScriptPlugin : ScriptPlugin
{
    /// <summary>The namespace this plugin owns.</summary>
    public const string Namespace = "system.session";

    public SystemSessionScriptPlugin()
        : base(BuildRegistry(), ownedNamespaces: Namespace)
    {
    }

    private static ReflectionMethodRegistry BuildRegistry()
    {
        var registry = new ReflectionMethodRegistry();
        registry.RegisterCallableMethods(new SessionService(), Namespace);
        return registry;
    }
}

/// <summary>
/// The concrete session accessors exposed under <c>system.session</c>. Guid-typed properties are
/// returned as strings (the language has no Guid type) and are <c>null</c> when the underlying value
/// is absent. The <see cref="IExecutionContext"/> parameter on each method is injected by the runtime
/// and hidden from the script-visible signature and generated <c>.d.ts</c>.
/// </summary>
internal sealed class SessionService
{
    // Resolves the host-registered session; throws a descriptive error if the host forgot to
    // register it (fail-fast, consistent with the service-locator convention).
    private static IEntityContext Session(IExecutionContext ctx) =>
        ctx.GetRequiredService<IEntityContext>();

    [ScriptCallable(Alias = "getUserId", Description = "Id of the user running the script (null if none).")]
    public string? GetUserId(IExecutionContext ctx) => Session(ctx).UserId?.ToString();

    [ScriptCallable(Alias = "getAccountId", Description = "Account id of the current session (null if none).")]
    public string? GetAccountId(IExecutionContext ctx) => Session(ctx).AccountId?.ToString();

    [ScriptCallable(Alias = "getOrganizationId", Description = "Organization id of the current session (null if none).")]
    public string? GetOrganizationId(IExecutionContext ctx) => Session(ctx).OrganizationId?.ToString();

    [ScriptCallable(Alias = "getProfileId", Description = "Profile id of the current session (null if none).")]
    public string? GetProfileId(IExecutionContext ctx) => Session(ctx).ProfileId?.ToString();

    [ScriptCallable(Alias = "getRole", Description = "Role of the current session.")]
    public string GetRole(IExecutionContext ctx) => Session(ctx).Role.ToString();

    [ScriptCallable(Alias = "getClientId", Description = "Client id of the current session.")]
    public string? GetClientId(IExecutionContext ctx) => Session(ctx).ClientId;

    [ScriptCallable(Alias = "getEntityId", Description = "Entity id of the current session (null if none).")]
    public string? GetEntityId(IExecutionContext ctx) => Session(ctx).EntityId?.ToString();
}
