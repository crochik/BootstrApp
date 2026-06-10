using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using LMS.Models;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace LMS.Handlers;

public class SaveRequestHandler : INewLeadHandler
{
    private readonly ILogger<SaveRequestHandler> _logger;
    private readonly MongoConnection _connection;

    public SaveRequestHandler(ILogger<SaveRequestHandler> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public Func<Context, ValueTask<Response>> Build(Func<Context, ValueTask<Response>> next) => async context =>
    {
        var transaction = new Transaction
        {
            Id = context.Request.Id,
            Request = context.Request,
        };

        // create request
        await _connection.InsertAsync(transaction);

        var result = default(Response);
        try
        {
            result = await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process lead");
            
            result = new Response
            {
                Reason = "Exception",
                Message = ex.Message,
            };
        }
        finally
        {
            if (!result.FinishedOn.HasValue)
            {
                if (context.Object == null || !context.Object.TryGetValue(nameof(Lead.LeadFee), out var costObject) || costObject is not decimal cost)
                {
                    cost = 0;
                }

                await _connection.Filter<Transaction>()
                    .Eq(x => x.Id, transaction.Id)
                    .Update
                    .Set(x => x.Response, result)
                    .SetOrUnset(x => x.AccountId, context.LeadType?.AccountId)
                    .SetOrUnset(x => x.EntityId, context.Entity?.Id ?? context.LeadType?.AccountId)
                    .Set(x => x.ParsedInput, context.Object)
                    .Set(x => x.Tags, context.Tags.ToArray())
                    .SetOrUnset(x => x.Refs, context.Refs)
                    .Set(x => x.AcceptedCost, result.Success ? cost : 0)
                    .Set(x => x.RejectedCost, !result.Success ? cost : 0)
                    .Set(x => x.Message, result.Message)
                    .UpdateOneAsync();
            }
        }

        return result;
    };
}