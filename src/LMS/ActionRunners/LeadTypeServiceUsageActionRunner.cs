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
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services.ActionRunners;

namespace LMS.ActionRunners;

public class LeadTypeServiceUsageActionRunner : AbstractRunner<LeadTypeServiceUsageActionOptions>
{
    private const string LeadTypeIdPath = "{{InitialObject.Request|LeadTypeId}}";
    private const string EntityIdPath = "{{Objects.Organization._id}}";
    private const string PostalCodePath = "{{Object.ParsedInput.PostalCode}}";
    private const string ServicePath = "{{Object.ParsedInput.Service}}";
    private const string LeadFeePath = "{{toDecimal Object.ParsedInput.LeadFee}}";

    private readonly ILogger<LeadTypeServiceUsageActionRunner> _logger;
    private readonly MongoConnection _connection;
    public override Guid ActionId => ActionIds.LeadTypeServiceUsage;

    public LeadTypeServiceUsageActionRunner(ILogger<LeadTypeServiceUsageActionRunner> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, LeadTypeServiceUsageActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);

        if (!TryGetGuid(context, runContext, LeadTypeIdPath, out var leadTypeId))
        {
            throw new BadRequestException("Can't find lead type id");
        }

        if (!TryGetGuid(context, runContext, EntityIdPath, out var organizationId))
        {
            throw new BadRequestException("Can't find Organization id");
        }

        if (!TryGet(context, runContext, PostalCodePath, out string postalCode))
        {
            postalCode = null;
        }

        if (!TryGet(context, runContext, ServicePath, out string service))
        {
            service = null;
        }

        var cost = TryGet(context, runContext, LeadFeePath, out decimal leadFee) ? leadFee : (decimal?)null;

        var tags = new List<string>();
        var result = await runChecksAsync();

        using var scope = _logger.AddScope(
            new
            {
                LeadTypeId = leadTypeId,
                OrganizationId = organizationId,
                PostalCode = postalCode,
                Service = service,
            }
        );

        if (tags.Count > 0 && context.ObjectType.Name == Transaction.ObjectTypeName)
        {
            await _connection.Filter<Transaction>()
                .Eq(x => x.Id, context.ObjectId)
                .Update
                .AddToSetEach(x => x.Tags, tags)
                .UpdateOneAsync();
        }

        if (result)
        {
            _logger.LogInformation("All service layers are active");
        }
        else
        {
            _logger.LogInformation("Service is disabled or over budget");
        }

        return getEvents().ToArray();

        async Task<Result<ServiceTracking>> runChecksAsync()
        {
            // source globally
            var result = await checkAsync(leadTypeId);
            if (!result)
            {
                tags.Add(LeadTypeServiceUsageActionOptions.TAG_OVER_BUDGET_SOURCE);
                return result;
            }

            // source for an org
            result = await checkAsync(leadTypeId, organizationId);
            if (!result)
            {
                tags.Add(LeadTypeServiceUsageActionOptions.TAG_OVER_BUDGET_ORG);
                return result;
            }
            
            if (!string.IsNullOrWhiteSpace(service))
            {
                // service globally
                result = await checkAsync(leadTypeId, null, service);
                if (!result)
                {
                    tags.Add(LeadTypeServiceUsageActionOptions.TAG_OVER_BUDGET_SERVICE);
                    return result;
                }
                
                // service for an org
                result = await checkAsync(leadTypeId, organizationId, service);
                if (!result)
                {
                    tags.Add(LeadTypeServiceUsageActionOptions.TAG_OVER_BUDGET_SERVICE);
                    return result;
                }
            }

            if (!string.IsNullOrWhiteSpace(postalCode))
            {
                // postal code for any service
                result = await checkAsync(null, organizationId, optPostalCode: postalCode);
                if (!result)
                {
                    tags.Add(LeadTypeServiceUsageActionOptions.TAG_OVER_BUDGET_POSTALCODE);
                    return result;
                }
                
                // postal code for service + org
                result = await checkAsync(leadTypeId, organizationId, optPostalCode: postalCode);
                if (!result)
                {
                    tags.Add(LeadTypeServiceUsageActionOptions.TAG_OVER_BUDGET_POSTALCODE);
                    return result;
                }
            }

            if (!string.IsNullOrWhiteSpace(service) && !string.IsNullOrWhiteSpace(postalCode))
            {
                // fine grained
                result = await checkAsync(leadTypeId, organizationId, service, postalCode);
                if (!result)
                {
                    tags.Add(LeadTypeServiceUsageActionOptions.TAG_OVER_BUDGET);
                    return result;
                }
            }

            return result;
        }

        IEnumerable<FlowEvent> getEvents()
        {
            if (result.IsSuccess)
            {
                var output = options.Output.FirstOrDefault(x => x.Name == LeadTypeServiceUsageActionOptions.OnBudgetEvent);
                if (output?.EventId.HasValue ?? false)
                {
                    yield return new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.LeadTypeServiceUsage),
                        EventTypeId = output.EventId,
                        Description = output.Description,
                    };
                }
            }
            else
            {
                var output = options.Output.FirstOrDefault(x => x.Name == LeadTypeServiceUsageActionOptions.OverBudgetEvent);
                if (output?.EventId.HasValue ?? false)
                {
                    yield return new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.LeadTypeServiceUsage),
                        EventTypeId = output.EventId,
                        Description = $"{output.Description}. {result.Status}"
                    };
                }
            }
        }

        async Task<Result<ServiceTracking>> checkAsync(Guid? optLeadTypeId, Guid? optEntityId = null, string optService = null, string optPostalCode = null)
        {
            using var scope1 = _logger.AddScope(new
            {
                LeadTypeId = optLeadTypeId,
                EntityId = optEntityId,
                Service = optService,
                PostalCode = optPostalCode
            });

            var now = DateTime.UtcNow;
            var tracking = await _connection.Filter<ServiceTracking>()
                .Eq(x => x.AccountId, context.EntityContext.AccountId)
                .Eq(x => x.LeadTypeId, optLeadTypeId)
                .Eq(x => x.EntityId, optEntityId)
                .Eq(x => x.Name, optService)
                .Eq(x => x.PostalCode, optPostalCode)
                .Update
                // .SetOnInsert(x => x.Id, Guid.NewGuid())
                .SetOnInsert(x => x.AccountId, context.EntityContext.AccountId)
                .SetOnInsert(x => x.LeadTypeId, optLeadTypeId)
                .SetOnInsert(x => x.EntityId, optEntityId)
                .SetOnInsert(x => x.Name, optService)
                .SetOnInsert(x => x.PostalCode, optPostalCode)
                .SetOnInsert(x => x.IsActive, true)
                .SetOnInsert(x => x.CreatedOn, now)
                // ExternalId
                // Description
                .Inc(x => x.Count, 1)
                .Set(x => x.LastModifiedOn, now)
                .UpdateAndGetOneAsync(true);

            if (!tracking.IsActive)
            {
                _logger.LogError("Service Disabled");
                return Result.Error<ServiceTracking>("Service disabled");
            }

            if (tracking.Constraints == null || (!tracking.Constraints.MaxLeads.HasValue && !tracking.Constraints.Budget.HasValue))
            {
                _logger.LogInformation("No constraints");

                if (tracking.Tags?.Length > 0)
                {
                    tags.AddRange(tracking.Tags);
                }

                return Result.Success(tracking);
            }

            switch (tracking.Constraints.BucketType)
            {
                case BucketType.Total:
                    break;

                case BucketType.Day:
                    // TODO: check bucket key and if doesn't match, reset count/total
                    // ...
                    break;
            }

            var query = _connection.Filter<ServiceTracking>()
                    .Eq(x => x.Id, tracking.Id)
                ;

            if (tracking.Constraints.MaxLeads.HasValue)
            {
                query.Lt(x => x.Constraints.Count, tracking.Constraints.MaxLeads);
            }

            if (tracking.Constraints.Budget.HasValue)
            {
                query.Lt(x => x.Constraints.Total, tracking.Constraints.Budget.Value - cost.GetValueOrDefault(0));
            }

            var tracking2 = await query.Update
                .Inc(x => x.Constraints.Count, 1)
                .Inc(x => x.Constraints.Total, cost.GetValueOrDefault(0))
                .Set(x => x.LastModifiedOn, now)
                .UpdateAndGetOneAsync();

            if (tracking2 != null)
            {
                if (tracking2.Tags?.Length > 0)
                {
                    tags.AddRange(tracking2.Tags);
                }

                _logger.LogInformation("Still within {Budget} {MaxLeads}: {Total} {Count}", tracking2.Constraints.Budget, tracking2.Constraints.MaxLeads, tracking2.Constraints.Total, tracking2.Constraints.Count);
                return Result.Success(tracking2);
            }

            // add ref to service tracking
            await _connection.Filter<Transaction>()
                .Eq(x => x.Id, context.ObjectId)
                .Update
                .Set(x => x.Refs[$"{ServiceTracking.ObjectTypeName}Id"], tracking.Id)
                .UpdateOneAsync();

            _logger.LogInformation("Over {Budget} {MaxLeads}: {Total} {Count}", tracking.Constraints.Budget, tracking.Constraints.MaxLeads, tracking.Constraints.Total, tracking.Constraints.Count);
            return Result.Error<ServiceTracking>("Over Budget/Total");
        }
    }
}