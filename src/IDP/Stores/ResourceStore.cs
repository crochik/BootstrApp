using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Logging;

namespace Stores;

public class ResourceStore : IResourceStore
{
    private readonly MongoConnection _connection;
    private readonly IMapper _mapper;
    private readonly ILogger<ResourceStore> _logger;

    public ResourceStore(MongoConnection connection, IMapper mapper, ILogger<ResourceStore> logger)
    {
        _connection = connection;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
    {
        _logger.LogInformation("FindIdentityResourcesByScopeNameAsync: {@Scopes}", scopeNames);
        var apis = await _connection.Filter<PI.Shared.Models.Client.IdentityResource>()
            .In(x => x.Name, scopeNames)
            .FindAsync();

        return _mapper.Map<IEnumerable<IdentityResource>>(apis);
    }

    public async Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames)
    {
        _logger.LogInformation("FindApiScopesByNameAsync: {@Scopes}", scopeNames);
        var apis = await _connection.Filter<PI.Shared.Models.Client.ApiResource>()
            .ElemMatchBuilder(x => x.Scopes, q => q.In(x => x.Name, scopeNames))
            .FindAsync();

        var scopes = apis.SelectMany(x => x.Scopes).Where(x => scopeNames.Contains(x.Name));
        return _mapper.Map<IEnumerable<ApiScope>>(scopes);
    }

    public async Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
    {
        _logger.LogInformation("FindApiResourcesByScopeNameAsync: {@Scopes}", scopeNames);
        var apis = await _connection.Filter<PI.Shared.Models.Client.ApiResource>()
            .ElemMatchBuilder(x => x.Scopes, q => q.In(x => x.Name, scopeNames))
            .FindAsync();

        return _mapper.Map<IEnumerable<ApiResource>>(apis);
    }

    public async Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames)
    {
        _logger.LogInformation("FindApiResourcesByNameAsync: {@Resources}", apiResourceNames);
        var apis = await _connection.Filter<PI.Shared.Models.Client.ApiResource>()
            .In(x => x.Name, apiResourceNames)
            .FindAsync();

        return _mapper.Map<IEnumerable<ApiResource>>(apis);
    }

    public async Task<Resources> GetAllResourcesAsync()
    {
        _logger.LogInformation("GetAllResourcesAsync");
        var identities = await _connection.Filter<PI.Shared.Models.Client.IdentityResource>().FindAsync();
        var apis = await _connection.Filter<PI.Shared.Models.Client.ApiResource>().FindAsync();

        var identityModels = identities.Select(x => _mapper.Map<IdentityResource>(x));
        // var identityModels = _mapper.Map<IEnumerable<IdentityResource>>(identities);
        var apiModels = _mapper.Map<IEnumerable<ApiResource>>(apis);

        var apiScopes = apis
            .SelectMany(x => x.Scopes)
            .Select(x => new ApiScope(x.Name, x.DisplayName, x.UserClaims?.Select(x => x.Type)));

        // var identiyScopes = identities.Select(x => new ApiScope(x.Name));

        var result = new Resources(identityModels, apiModels, apiScopes);

        return result;
    }
}