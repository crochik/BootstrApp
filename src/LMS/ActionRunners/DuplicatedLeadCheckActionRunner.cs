using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using LMS.Models;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Services.ActionRunners;

public class DuplicatedLeadCheckActionRunner : AbstractRunner<DuplicatedLeadCheckActionOptions>
{
    private readonly ILogger<DuplicatedLeadCheckActionRunner> _logger;
    private readonly MongoConnection _connection;

    // for now makes a lot of assumptions about the parsed data
    private const string PhoneNumberPath = "{{normalizePhone Object.ParsedInput.Phone}}";
    private const string EmailPath = "{{normalizeEmail Object.ParsedInput.Email}}";
    private const string EntityIdPath = "{{Objects.Organization._id}}";

    public override Guid ActionId => ActionIds.DuplicatedLeadCheck;

    public DuplicatedLeadCheckActionRunner(ILogger<DuplicatedLeadCheckActionRunner> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, DuplicatedLeadCheckActionOptions options)
    {
        if (context.ObjectType.Name != Transaction.ObjectTypeName)
        {
            throw new BadRequestException("Runner not yet available for this object type");
        }

        var runContext = context.Run.BuildHandlebarsContext(context.Event);

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, PhoneNumberPath, out var phoneNumberObj) || phoneNumberObj is not string normalizedPhone || string.IsNullOrWhiteSpace(normalizedPhone))
        {
            _logger.LogInformation("Phone number not provided");
            normalizedPhone = null;
        }

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, EmailPath, out var emailObj) || emailObj is not string normalizedEmail || string.IsNullOrWhiteSpace(normalizedEmail))
        {
            _logger.LogInformation("Email not provided");
            normalizedEmail = null;
        }

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, EntityIdPath, out var entityIdObj) || entityIdObj is not string entityIdStr || !Guid.TryParse(entityIdStr, out var entityId))
        {
            throw new BadRequestException("Couldn't determine Organization");
        }

        if (normalizedEmail == null && normalizedPhone == null)
        {
            throw new BadRequestException("Missing required fields");
        }

        var tags = default(string[]);
        
        var duplicatedLead = await findExisting();
        if (duplicatedLead == null)
        {
            duplicatedLead = await findExisting(true);
            if (duplicatedLead != null)
            {
                _logger.LogInformation("Found suppressed {LeadId}", duplicatedLead.Id);
                tags = [DuplicatedLeadCheckActionOptions.TAG_DUPLICATE, DuplicatedLeadCheckActionOptions.TAG_SUPPRESSED];
            }
            else
            {
                _logger.LogInformation("Didn't find Duplicate for Lead");    
            }
        }
        else
        {
            _logger.LogInformation("Found a duplicate for Lead: {LeadId}", duplicatedLead.Id);
            tags = [DuplicatedLeadCheckActionOptions.TAG_DUPLICATE];
        }

        // if (duplicatedLead != null)
        // {
            // TODO: add ref to run?
            // await _connection.Filter<FlowRun>()
            //     .Eq(x=>x.Id, context.Run.Id)
            //     .Update
            //     .Set(x=>x.Refs)
        // }

        if (duplicatedLead != null && context.ObjectType.Name == Transaction.ObjectTypeName)
        {
            await _connection.Filter<Transaction>()
                .Eq(x => x.Id, context.ObjectId)
                .Update
                .Set(x => x.Refs["LeadId"], duplicatedLead.Id)
                .AddToSetEach(x => x.Tags, tags)
                .UpdateOneAsync();
        }

        return getEvents().ToArray();

        IEnumerable<FlowEvent> getEvents()
        {
            if (duplicatedLead == null)
            {
                yield return new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.DuplicatedLeadCheck),
                    EventTypeId = options.NextEventId,
                    Description = options.GetEventDescription(options.NextEventId) ?? "No duplicates found for lead",
                };
                yield break;
            }

            if (options.DuplicateLeadEventId.HasValue)
            {
                var evt = new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.DuplicatedLeadCheck),
                    EventTypeId = options.DuplicateLeadEventId,
                    Description = options.GetEventDescription(options.DuplicateLeadEventId) ?? "Lead is a duplicate",
                };
                evt.AddRefValue(duplicatedLead);
                evt.SetMetaValue("Action|Output|DuplicateOfLeadId", duplicatedLead.Id);
                yield return evt;
            }

            // TODO: should it fire for the original lead (like the current behavior?)
            // ...

            if (options.AlwaysFireNextEvent)
            {
                var evt = new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.DuplicatedLeadCheck),
                    EventTypeId = options.NextEventId,
                    Description = "Lead is a duplicate, ignore and continue...",
                };

                evt.AddRefValue(duplicatedLead);
                evt.SetMetaValue("Action|Output|DuplicateOfLeadId", duplicatedLead.Id);
                yield return evt;
            }
        }

        async Task<Lead> findExisting(bool checkSuppressed = false)
        {
            var query = _connection.Filter<Lead>()
                    .Eq(x => x.AccountId, context.EntityContext.AccountId)
                    .Eq(x => x.EntityId, entityId)
                ;

            if (checkSuppressed)
            {
                query.Eq(x => x.IsSuppressed, true);
            }
            else
            {
                var minDate = options.Offset.HasValue ? DateTime.UtcNow - options.Offset.Value : DateTime.UtcNow.AddDays(-30);
                query.Gt(x => x.CreatedOn, minDate);
            }

            if (normalizedEmail != null)
            {
                if (normalizedPhone != null)
                {
                    query.OrBuilder(
                        q => q.Eq(x => x.NormalizedPhoneNumber, normalizedPhone),
                        q => q.Eq(x => x.NormalizedEmail, normalizedEmail)
                    );
                }
                else
                {
                    query.Eq(x => x.NormalizedEmail, normalizedEmail);
                }
            }
            else
            {
                query.Eq(x => x.NormalizedPhoneNumber, normalizedPhone);
            }

            var lead = await query
                .SortAsc(x => x.CreatedOn)
                .FirstOrDefaultAsync();
            
            return lead;
        }
    }
}