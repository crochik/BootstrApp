using System;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using LMS.Models;
using Microsoft.Extensions.Logging;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace LMS.Handlers;

public class LoadLeadTypeHandler : INewLeadHandler
{
    private readonly ILogger<LoadLeadTypeHandler> _logger;
    private readonly MongoConnection _connection;

    public LoadLeadTypeHandler(ILogger<LoadLeadTypeHandler> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public Func<Context, ValueTask<Response>> Build(Func<Context, ValueTask<Response>> next) => async r =>
    {
        r.LeadType = await _connection.Filter<LeadType>().Eq(x => x.Id, r.Request.LeadTypeId).FirstOrDefaultAsync();
        if (r.LeadType == null)
        {
            return new Response
            {
                Reason = "BAD_CONFIG",
                Message = "Invalid Lead Type",
            };
        }

        r.Entity = await _connection.Filter<Entity>()
            .Eq(x => x.Id, r.LeadType.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (r.LeadType == null)
        {
            return new Response
            {
                Reason = "BAD_CONFIG",
                Message = "Invalid Entity",
            };
        }

        using var scope = _logger.AddScope(new
        {
            LeadTypeId = r.LeadType.Id,
            LeadType = r.LeadType.Name,
        });
        
        _logger.LogInformation("Loaded Lead Type");

        return await next(r);
    };
}