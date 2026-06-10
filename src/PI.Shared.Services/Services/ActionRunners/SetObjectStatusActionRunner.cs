using System;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace PI.Shared.Services.ActionRunners;

public class SetObjectStatusActionRunner : AbstractObjectRunner<SetObjectStatusActionOptions>
{
    public override Guid ActionId => ActionIds.SetObjectStatus;

    public SetObjectStatusActionRunner(ILogger<SetObjectStatusActionRunner> logger, MongoConnection connection, ObjectTypeService objectTypeService)
        : base(logger, connection, objectTypeService)
    {
    }

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, SetObjectStatusActionOptions options)
    {
        using var scope = _logger.AddScope(new
        {
            ObjectType = context.ObjectType.Name,
            context.ObjectId,
            options.ObjectStatusId,
        });

        if (options.ObjectStatusId.HasValue)
        {
            var objectStatus = await _connection.Filter<ObjectStatus>()
                .Eq(x => x.AccountId, context.EntityContext.AccountId)
                .Eq(x => x.ObjectType, context.ObjectType.FullName)
                .Eq(x => x.Id, options.ObjectStatusId)
                .FirstOrDefaultAsync();

            if (objectStatus == null) throw NotFoundException.New<ObjectStatus>(options.ObjectStatusId.Value);

            _logger.LogInformation("Change Status to {ObjectStatus}", objectStatus.Name);

            var isModified = await _objectTypeService.UpdateObjectStatusAsync(context.EntityContext, context.ObjectType, context.ObjectId, options.ObjectStatusId.Value);
            if (isModified)
            {
                _logger.LogInformation("Successfully updated object status");

                return new FlowEvent[]
                {
                    new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.SetObjectStatus),
                        Description = $"Status updated to {objectStatus.Name}",
                        EventTypeId = EventIds.OnStatusEntered,
                    }
                };
            }

            _logger.LogInformation("Nothing to change");
        }
        else
        {
            _logger.LogError("Missing required Object Status Id");
        }

        return Array.Empty<FlowEvent>();
    }
}