using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IDP;

/// <summary>
/// Resolves auth scheme names like "Google:&lt;clientId&gt;" on demand by looking up the AppClient
/// via <see cref="ProviderResolver"/>. Bare scheme names go through the base implementation.
/// </summary>
public class DynamicAuthenticationSchemeProvider : AuthenticationSchemeProvider
{
    private readonly IServiceProvider _services;
    private readonly ConcurrentDictionary<string, AuthenticationScheme> _dynCache = new();
    private readonly object _addLock = new();

    public DynamicAuthenticationSchemeProvider(IOptions<AuthenticationOptions> options, IServiceProvider services)
        : base(options)
    {
        _services = services;
    }

    public override async Task<AuthenticationScheme> GetSchemeAsync(string name)
    {
        var scheme = await base.GetSchemeAsync(name);
        if (scheme != null) return scheme;
        if (!SchemeName.IsComposite(name)) return null;
        if (_dynCache.TryGetValue(name, out var cached)) return cached;

        var resolver = _services.GetRequiredService<ProviderResolver>();
        var built = await resolver.TryBuildSchemeAsync(name);
        if (built == null) return null;

        lock (_addLock)
        {
            if (_dynCache.TryGetValue(name, out var existing)) return existing;
            try
            {
                AddScheme(built);
            }
            catch (InvalidOperationException)
            {
                // race: another caller registered the same scheme between checks; safe to ignore.
            }
            _dynCache[name] = built;
        }
        return built;
    }
}
