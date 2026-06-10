using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared;
using PI.Shared.Data.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class LeadTypeMapper
{
    private readonly ILogger<LeadTypeMapper> _logger;
    private readonly MongoConnection _connection;

    public LeadTypeMapper(
        ILogger<LeadTypeMapper> logger,
        MongoConnection connection
    )
    {
        _logger = logger;
        _connection = connection;
    }

    public async Task<List<FieldMapperConfig>> RefreshMapAsync(IEntityContext context, LeadType leadType)
    {
        var records = await _connection.Filter<LeadRequest>()
            .Eq(x => x.LeadTypeId, leadType.Id)
            .Limit(100)
            .FindAsync();

        // var records = await leadAdapter.GetByTypeAsync(context, leadType.Id, new QueryParams(100, $"{nameof(Lead.CreatedOn)} DESC"));

        var builder = new FieldMapBuilder(leadType.Settings?.Fields);
        return builder.Add(records.Select(x => x.Body));
    }
}