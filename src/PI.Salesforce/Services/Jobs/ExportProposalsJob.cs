using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services.Jobs;

public class ExportProposalsJob : IRunJob
{
    public string Name => "ExportProposals";

    private readonly ILogger<ExportProposalsJob> _logger;
    private readonly MongoConnection _connection;
    private readonly IMessageBroker _messageBroker;
    private readonly Configuration _configuration;

    public ExportProposalsJob(
        ILogger<ExportProposalsJob> logger,
        MongoConnection connection,
        IMessageBroker messageBroker,
        IConfiguration configuration
    )
    {
        _logger = logger;
        _connection = connection;
        _messageBroker = messageBroker;

        _configuration = configuration.GetSection(nameof(ExportProposalsJob)).Get<Configuration>();
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        if (_configuration==null || !_configuration.FlowId.HasValue || !_configuration.ObjectStatusId.HasValue || !_configuration.EventId.HasValue) throw new Exception("Missing configuration");
        
        var start = DateTime.UtcNow.AddDays(-2);
        
        var cursor = _connection.Filter<SfOptionObject>("salesforce.INET_Option__c")
            .Eq(x => x.AccountId, context.AccountId)
            .Gt(x => x.Properties["LastModifiedDate"], start)
            .Eq(x => x.Properties["Is_Exported__c"], true)
            .Eq(x => x.FileExportedOn, null)
            .SortAsc(x => x.Properties["LastModifiedDate"])
            .ToCursor();

        var count = 0;
        var changed = 0;
        while (await cursor.MoveNextAsync(stoppingToken))
        {
            if (_configuration.Count > 0 && count > _configuration.Count)
            {
                break;
            }
            
            foreach (var row in cursor.Current)
            {
                count++;
                if (_configuration.Count > 0 && count > _configuration.Count)
                {
                    _logger.LogInformation("Pausing after {Count}", _configuration.Count);
                    break;
                }
 
                using var scope = _logger.AddScope(new
                {
                    OptionId = row.Id,
                    row.ExternalId,
                });

                _logger.LogInformation("Export Proposal");

                var obj = row;
                if (!row.FlowId.HasValue)
                {
                    obj = await _connection.Filter<SfOptionObject>("salesforce.INET_Option__c")
                        .Eq(x => x.AccountId, context.AccountId)
                        .Eq(x => x.Id, row.Id)
                        .Update
                        .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                        .Set(x => x.LastActor, context.Actor())
                        .Set(x => x.FlowId, _configuration.FlowId)
                        .Set(x => x.ObjectStatusId, _configuration.ObjectStatusId)
                        .UpdateAndGetOneAsync();

                    _logger.LogInformation("Updated flow");
                    changed++;
                }

                _logger.LogInformation("Fire Event");

                var evt = new GenericFlowEvent(obj)
                {
                    Description = "Proposal was exported by user",
                    EventTypeId = _configuration.EventId,
                };

                await _messageBroker.DispatchAsync(evt);
            }
        }

        return new JobResult
        {
            Message = $"Triggered flow for {changed} option(s).",
            Result = new Dictionary<string, object>
            {
                { "ModifiedCount", changed },
                { "Total", count },
                // { "NotFoundCount", notfound },
            },
        };
    }

    private class SfOptionObject : SalesforceCustomObject
    {
        public DateTime? FileExportedOn { get; set; }
    }

    private class Configuration
    {
        public Guid? FlowId { get; set; }
        public Guid? ObjectStatusId { get; set; }
        public Guid? EventId { get; set; }
        public int Count { get; set; } = 10;
    }
}
