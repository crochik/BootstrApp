using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services.Jobs;

public class AssignWorkOrdersJob : IRunJob
{
    private readonly ILogger<AssignWorkOrdersJob> _logger;
    private readonly MongoConnection _connection;
    public string Name => "AssignWorkOrders";

    public AssignWorkOrdersJob(ILogger<AssignWorkOrdersJob> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var cursor = _connection.Filter<SfWorkOrderObject>("salesforce.WorkOrder")
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.LeadId, null)
            .Gt(x => x.LastModifiedOn, DateTime.UtcNow.AddDays(-2))
            .SortAsc(x => x.LastModifiedOn)
            .ToCursor();

        var list = new List<UpdateOneModel<SfWorkOrderObject>>();
        var changed = 0L;
        var count = 0L;
        var notfound = 0L;
        while (await cursor.MoveNextAsync(stoppingToken))
        {
            foreach (var row in cursor.Current)
            {
                count++;

                using var scope = _logger.AddScope(new
                {
                    WorkOrderId = row.Id,
                    row.ExternalId,
                    row.EntityId,
                    row.AssignedEntityId
                });

                var lead = default(Lead);

                if (row.TryGetProperty<string>(SfWorkOrderObject.AccountIdField, out var accountId))
                {
                    _logger.LogInformation("Try to find lead for {SfAccountId}", accountId);

                    lead = await _connection.Filter<Lead>()
                        .Eq(x => x.AccountId, context.AccountId)
                        .ElemMatchBuilder(x => x.Integrations, q => q
                            .Eq(x => x.ExternalId, accountId)
                            .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
                        )
                        .FirstOrDefaultAsync();
                }

                // FCI ONLY, fallback 
                if (lead == null && row.TryGetProperty<string>(SfWorkOrderObject.LeadIdField, out var leadId))
                {
                    _logger.LogInformation("Try to find lead for {SfLeadId}", leadId);

                    lead = await _connection.Filter<Lead>()
                        .Eq(x => x.AccountId, context.AccountId)
                        .ElemMatchBuilder(x => x.Integrations, q => q
                            .Eq(x => x.ExternalId, leadId)
                            .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
                        )
                        .FirstOrDefaultAsync();
                }

                if (lead != null)
                {
                    _logger.LogInformation("Assign {LeadId} {EntityId}", lead.Id, lead.EntityId);

                    var model = _connection.Filter<SfWorkOrderObject>("salesforce.WorkOrder")
                        .Eq(x => x.AccountId, context.AccountId)
                        .Eq(x => x.Id, row.Id)
                        .Eq(x => x.LeadId, null)
                        .Update
                        .Set(x => x.LeadId, lead.Id)
                        .Set(x => x.EntityId, lead.EntityId)
                        .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                        .UpdateOneModel();

                    list.Add(model);
                    await commitAsync();
                }
                else
                {
                    _logger.LogInformation("Couldn't find Lead for WorkOrder");
                    notfound++;
                }
            }
        }

        await commitAsync(true);

        return new JobResult
        {
            Message = $"Updated {changed} work orders.",
            Result = new Dictionary<string, object>
            {
                { "ModifiedCount", changed },
                { "Total", count },
                { "NotFoundCount", notfound },
            },
        };

        async Task commitAsync(bool force = false)
        {
            if (!force && list.Count < 100) return;
            if (list.IsEmpty()) return;

            var result = await _connection.BulkWriteAsync("salesforce.WorkOrder", list);
            _logger.LogInformation("{ModifiedCount} WorkOrder(s)", result.ModifiedCount);
            changed += result.ModifiedCount;
            list.Clear();
        }
    }
}