using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Crochik.Mongo;
using IdentityServer4.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Services;

public class CorsPolicyService : ICorsPolicyService
{
    private readonly ILogger<CorsPolicyService> _logger;
    private readonly MongoConnection _connection;

    public CorsPolicyService(ILogger<CorsPolicyService> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public async Task<bool> IsOriginAllowedAsync(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return true;

        var query = _connection.Filter<PI.Shared.Models.AppClient>();
        
        var hosts = DeriveHosts(origin);
        if (hosts.IsEmpty())
        {
            var regex = new BsonRegularExpression($"^{Regex.Escape(origin)}$", "i");
            query.ElemMatchBuilder(x => x.AllowedCorsOrigins, q => q.Regex(x => x.Origin, regex));
        }
        else
        {
            query.ElemMatchBuilder(x => x.AllowedCorsOrigins, q => q.In(x => x.Origin, hosts));
        }
        
        var client = await query.FirstOrDefaultAsync();
        if (client == null) return false;
        
        _logger.LogInformation("Origin {origin} is allowed for {ClientId}", origin, client?.ClientId);
        return true;
    }
    
    public static IEnumerable<string> DeriveHosts(string origin)
    {
        origin = origin.ToLowerInvariant();
        if (!Uri.TryCreate(origin, UriKind.RelativeOrAbsolute, out var uri)) yield break;

        yield return origin;
        yield return $"https://{uri.Host}";
        yield return uri.Host;
                
        var parts = uri.Host.Split(".");
        if (parts.Length > 2)
        {
            yield return "*." + string.Join(".", parts[1..]);
        }
    }
}