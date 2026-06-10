using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Logging;

namespace Stores;

public class PersistedGrantStore : IPersistedGrantStore
{
    private readonly MongoConnection _connection;
    private readonly IMapper _mapper;
    private readonly ILogger<PersistedGrantStore> _logger;

    public PersistedGrantStore(MongoConnection connection, IMapper mapper, ILogger<PersistedGrantStore> logger)
    {
        _connection = connection;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task StoreAsync(PersistedGrant grant)
    {
        var existing = await _connection.Filter<PI.Shared.Models.Client.PersistedGrant>()
            .Eq(x => x.Key, grant.Key)
            .FirstOrDefaultAsync();

        if (existing == null)
        {
            _logger.LogDebug("{PersistedGrantKey} not found in database", grant.Key);

            var persistedGrant = _mapper.Map<PI.Shared.Models.Client.PersistedGrant>(grant);
            await _connection.InsertAsync(persistedGrant);
        }
        else
        {
            _logger.LogDebug("{PersistedGrantKey} found in database", grant.Key);

            var persistedGrant = _mapper.Map(grant, existing);

            await _connection.Filter<PI.Shared.Models.Client.PersistedGrant>()
                .Eq(x => x.Key, persistedGrant.Key)
                .ReplaceAndGetOneAsync(persistedGrant);
        }
    }

    public async Task<PersistedGrant> GetAsync(string key)
    {
        var persistedGrant = await _connection.Filter<PI.Shared.Models.Client.PersistedGrant>()
            .Eq(x => x.Key, key)
            .FirstOrDefaultAsync();

        var model = _mapper.Map<PersistedGrant>(persistedGrant);

        _logger.LogDebug("{PersistedGrantKey} found in database: {PersistedGrantKeyFound}", key, model != null);

        return model;
    }

    public async Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
    {
        var query = _connection.Filter<PI.Shared.Models.Client.PersistedGrant>();
        
        if (string.IsNullOrWhiteSpace(filter.Type))
        {
            query.Eq(x => x.Type, filter.Type);
        }
        if (string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query.Eq(x => x.SubjectId, filter.SubjectId);
        }
        if (string.IsNullOrWhiteSpace(filter.ClientId))
        {
            query.Eq(x => x.ClientId, filter.ClientId);
        }

        var persistedGrants = await query.FindAsync();
        var model = _mapper.Map<IEnumerable<PersistedGrant>>(persistedGrants);

        _logger.LogDebug("{PersistedGrantCount} persisted grants found", persistedGrants.Count);

        return model;
    }

    // public async Task<IEnumerable<PersistedGrant>> GetAllAsync(string subjectId)
    // {
    //     var persistedGrants = await _connection.Filter<PI.Shared.Models.Client.PersistedGrant>()
    //         .Eq(x => x.SubjectId, subjectId)
    //         .FindAsync();
    //
    //     var model = _mapper.Map<IEnumerable<PersistedGrant>>(persistedGrants);
    //
    //     _logger.LogDebug("{persistedGrantCount} persisted grants found for {subjectId}", persistedGrants.Count, subjectId);
    //
    //     return model;
    // }

    public Task RemoveAllAsync(PersistedGrantFilter filter)
    {
        var query = _connection.Filter<PI.Shared.Models.Client.PersistedGrant>();
        
        if (string.IsNullOrWhiteSpace(filter.Type))
        {
            query.Eq(x => x.Type, filter.Type);
        }
        if (string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query.Eq(x => x.SubjectId, filter.SubjectId);
        }
        if (string.IsNullOrWhiteSpace(filter.ClientId))
        {
            query.Eq(x => x.ClientId, filter.ClientId);
        }

        return query.DeleteAsync();
    }

    public async Task RemoveAsync(string key)
    {
        var result = await _connection.Filter<PI.Shared.Models.Client.PersistedGrant>()
            .Eq(x => x.Key, key)
            .DeleteOneAsync();

        if (result)
        {
            _logger.LogDebug("removed {PersistedGrantKey} persisted grant from database", key);
        }
        else
        {
            _logger.LogDebug("no {PersistedGrantKey} persisted grant found in database", key);
        }
    }
    
    // public async Task RemoveAllAsync(string subjectId, string clientId)
    // {
    //     var result = await _connection.Filter<PI.Shared.Models.Client.PersistedGrant>()
    //         .Eq(x => x.SubjectId, subjectId)
    //         .Eq(x => x.ClientId, clientId)
    //         .DeleteOneAsync();
    //
    //     _logger.LogDebug("removed {persistedGrantCount} persisted grants from database for subject {subjectId}, clientId {clientId}", result, subjectId, clientId);
    // }
    //
    // public async Task RemoveAllAsync(string subjectId, string clientId, string type)
    // {
    //     var result = await _connection.Filter<PI.Shared.Models.Client.PersistedGrant>()
    //         .Eq(x => x.SubjectId, subjectId)
    //         .Eq(x => x.ClientId, clientId)
    //         .Eq(x => x.Type, type)
    //         .DeleteAsync();
    //
    //     _logger.LogDebug("removed {persistedGrantCount} persisted grants from database for subject {subjectId}, clientId {clientId}, grantType {persistedGrantType}", result, subjectId, clientId, type);
    // }
}