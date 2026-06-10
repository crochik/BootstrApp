using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Logging;

namespace Stores;

public class ClientStore : IClientStore
{
    private readonly MongoConnection _connection;
    private readonly IMapper _mapper;
    private readonly ILogger<ClientStore> _logger;

    public ClientStore(MongoConnection connection, IMapper mapper, ILogger<ClientStore> logger)
    {
        _connection = connection;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Client> FindClientByIdAsync(string clientId)
    {
        var client = await _connection.Filter<PI.Shared.Models.AppClient>().Eq(x=>x.ClientId, clientId).FirstOrDefaultAsync();
        var model = _mapper.Map<Client>(client);
        _logger.LogDebug("{clientId} found in database: {clientIdFound}", clientId, model != null);

        return model;
    }
}