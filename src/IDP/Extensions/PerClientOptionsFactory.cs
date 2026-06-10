using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PI.Shared.Models;

namespace IDP;

/// <summary>
/// IOptionsFactory that resolves auth handler options per AppClient when the scheme name is composite
/// ("Provider:ClientId"). For bare scheme names it delegates to the default factory so existing
/// global registrations in <see cref="ServicesExtensions"/> behave exactly as before.
/// </summary>
public class PerClientOptionsFactory<TOptions> : IOptionsFactory<TOptions>
    where TOptions : AuthenticationSchemeOptions, new()
{
    private readonly IOptionsFactory<TOptions> _inner;
    private readonly ProviderResolver _resolver;
    private readonly MongoConnection _mongo;
    private readonly IEnumerable<IPostConfigureOptions<TOptions>> _postConfigures;

    public PerClientOptionsFactory(
        ProviderResolver resolver,
        MongoConnection mongo,
        IEnumerable<IConfigureOptions<TOptions>> configures,
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures)
    {
        _resolver = resolver;
        _mongo = mongo;
        _postConfigures = postConfigures;
        // Default factory for the bare-scheme path (mirrors framework wiring).
        _inner = new OptionsFactory<TOptions>(configures, postConfigures);
    }

    public TOptions Create(string name)
    {
        if (!SchemeName.IsComposite(name)) return _inner.Create(name);

        var (providerKey, clientId) = SchemeName.Split(name);

        var client = _mongo.Filter<AppClient>().Eq(x => x.ClientId, clientId)
            .FirstOrDefaultAsync().GetAwaiter().GetResult();
        var ap = client?.AuthenticationProviders == null
            ? null
            : (client.AuthenticationProviders.TryGetValue(providerKey, out var entry) ? entry : null);
        if (ap == null)
            throw new InvalidOperationException($"Unknown per-client scheme '{name}'");
        if (string.IsNullOrEmpty(ap.ClientId))
            throw new InvalidOperationException($"AppClient '{clientId}' has no ClientId for provider '{providerKey}'");

        var desc = _resolver.ResolveDescriptor(providerKey, ap);
        var opts = new TOptions();
        desc.Configure(ap, opts);

        if (opts is RemoteAuthenticationOptions ro)
        {
            ro.SignInScheme = Consts.ExternalCookieAuthenticationScheme;
            ro.CallbackPath = !string.IsNullOrEmpty(ap.CallbackPath)
                ? ap.CallbackPath
                : $"/signin/{providerKey}/{clientId}";
        }

        // Run post-configure (e.g. IdentityServer's OpenIdConnect post-configure) so the per-client
        // options go through the same finalization as the global ones.
        foreach (var post in _postConfigures)
        {
            post.PostConfigure(name, opts);
        }
        return opts;
    }
}
