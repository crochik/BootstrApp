using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using LMS.Models;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services.ActionRunners;
using Context = LMS.Models.Context;

namespace LMS.Handlers;

public class Version2InterceptorHandler : INewLeadHandler
{
    private readonly ILogger<Version2InterceptorHandler> _logger;
    private readonly ActionRunnerService _service;
    private readonly MongoConnection _connection;

    public Version2InterceptorHandler(ILogger<Version2InterceptorHandler> logger, ActionRunnerService service, MongoConnection connection)
    {
        _logger = logger;
        _service = service;
        _connection = connection;
    }

    public Func<Context, ValueTask<Response>> Build(Func<Context, ValueTask<Response>> next) => async (r) =>
    {
        if (r.LeadType.TransactionFlowId.HasValue && r.LeadType.TransactionObjectStatusId.HasValue)
        {
            return await RunActionServiceAsync(r);
        }

        return new Response
        {
            Reason = "TRAINING",
            Message = "Lead Type not configured",
        };
        // return await next(r);
    };

    private async Task<Response> RunActionServiceAsync(Context context)
    {
        using var scope = _logger.AddScope(new
        {
            TransactionId = context.Request.Id,
            LeadTypeId = context.LeadType.Id,
            context.LeadType.TransactionObjectStatusId,
            context.LeadType.TransactionFlowId,
        });

        _logger.LogInformation("Import Lead using Flow (v2)");

        var transaction = await _connection.Filter<Transaction>()
            .Eq(x => x.Id, context.Request.Id)
            .Update
            .Set(x => x.AccountId, context.LeadType.AccountId)
            .Set(x => x.FlowId, context.LeadType.TransactionFlowId)
            .Set(x => x.ObjectStatusId, context.LeadType.TransactionObjectStatusId)
            .UpdateAndGetOneAsync();

        var initialEvent = new GenericFlowEvent
        {
            AccountId = context.Entity.AccountId,
            ObjectType = Transaction.ObjectTypeName,
            TargetId = transaction.Id,
            EventTypeId = EventIds.OnStatusEntered,
            FlowId = transaction.FlowId.Value,
            StatusId = transaction.ObjectStatusId.Value,
        };

        var entityContext = context.Entity.Context;
        await _service.RunAsync(entityContext, initialEvent);

        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.Id, initialEvent.RunId)
            .IncludeFields(x => x.Id, x => x.Steps)
            .FirstOrDefaultAsync();

        var log = flowRun.Steps
            .Select(x => string.IsNullOrWhiteSpace(x.Event.Action) ? x.Event.Description : $"{x.Event.Action}: {x.Event.Description}")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        transaction = await _connection.Filter<Transaction>()
            .Eq(x => x.Id, context.Request.Id)
            .Update
            .Set(x => x.Message, string.Join('\n', log))
            .Set(x => x.Response.FinishedOn, DateTime.UtcNow)
            .Set(x => x.Refs[nameof(FlowRun)], initialEvent.RunId)
            .UpdateAndGetOneAsync();

        return transaction.Response;
    }
}