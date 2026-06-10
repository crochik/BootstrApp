using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using HandlebarsDotNet;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace PI.Shared.Services.ActionRunners;

public class ComposeActionRunner(ILogger<ComposeActionRunner> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    : AbstractObjectRunner<ComposeActionOptions>(logger, connection, objectTypeService)
{
    public override Guid ActionId => ActionIds.Compose;

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, ComposeActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);
        var hb = Handlebars.Create();

        try
        {
            var result = hb.Compile(options.Template).Invoke(runContext);
            return successEvents(result).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template");
            return errorEvents(ex.Message).ToArray();
        }


        IEnumerable<FlowEvent> successEvents(string result)
        {
            var output = options.Output.FirstOrDefault(x => x.Name == ComposeActionOptions.ContentCreatedEventName);
            if (output?.EventId.HasValue ?? false)
            {
                var evt = new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.Compose),
                    Description = output.Description,
                    EventTypeId = output.EventId,
                };
                evt.SetMetaValue(options.Alias, result);

                yield return evt;
            }
        }

        IEnumerable<FlowEvent> errorEvents(string error)
        {
            var output = options.Output.FirstOrDefault(x => x.Name == ComposeActionOptions.FailedToCreateContentEventName);
            if (output?.EventId.HasValue ?? false)
            {
                yield return new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.Compose),
                    Description = $"{output.Description}. {error}",
                    EventTypeId = output.EventId,
                };
            }
        }
    }
}