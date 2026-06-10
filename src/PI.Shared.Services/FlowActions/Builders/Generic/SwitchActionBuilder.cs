using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services;

namespace FlowActions;

public class SwitchActionBuilder : AbstractGenericActionBuilder
{
    public override Guid Id => ActionIds.Switch;
    public override string Name => "Switch";
    public override string IconName => null;
    public override string Description => "Decision Maker";
    public override string[] InputObjectTypes => null;
    protected override string ActionOptionsObjectType => nameof(SwitchActionOptions);

    public override bool IsValidTrigger(string objectType) => true;

    public SwitchActionBuilder(ObjectTypeService objectTypeService) : base(objectTypeService)
    {
    }

    protected override GenericActionOptions CalculateGenericOptions(ExpandoObject raw, GenericActionOptions current = null)
    {
        // TODO: use previous option to keep changes
        // ...
        
        // recalculate outputs 
        var options = GenericActionOptions.Convert<IDictionary<string, object>, SwitchActionOptions>(raw);

        var previousOptions = default(SwitchActionOptions);
        if (current != null)
        {
            previousOptions = GenericActionOptions.Convert<IDictionary<string, object>, SwitchActionOptions>(current.Raw);

            options.DefaultEventId = previousOptions.DefaultEventId;
        }

        // copy previous eventIds
        var previousCases = previousOptions?.Cases.ToDictionary(x => x.Name);
        if (previousCases != null)
        {
            foreach (var c in options.Cases)
            {
                if (previousCases.TryGetValue(c.Name, out var previous))
                {
                    c.EventId = previous.EventId;
                }
            }
        }
        
        var outputs = options.Cases
            .Select((x, i) => new ActionOutput
            {
                EventId = x.EventId,
                Name = $"Case{i + 1}",
                Description = x.Name,
            })
            .Append(new ActionOutput
            {
                EventId = options.DefaultEventId,
                Name = "Default",
                Description = options.DefaultCase,
            })
            .ToArray();
        
        // copy colors
        if (current?.Output != null)
        {
            var previousOutputs = current.Output.ToDictionary(x => x.EventId);
            foreach (var output in outputs)
            {
                if (output.EventId.HasValue && previousOutputs.TryGetValue(output.EventId.Value, out var previous))
                {
                    output.Color = previous.Color;
                }
            }
        }

        return new GenericActionOptions
        {
            Raw = GenericActionOptions.Convert<SwitchActionOptions, ExpandoObject>(options),
            Output = outputs,
        };
    }

    public override ValueTask SwapAsync(IEntityContext context, FlowStep step, Dictionary<Guid, Guid?> swap)
    {
        if (step.Options is not GenericActionOptions genericActionOptions) return ValueTask.CompletedTask;
        var options = GenericActionOptions.Convert<IDictionary<string, object>, SwitchActionOptions>(genericActionOptions.Raw);

        // cases 
        var modified = false;
        foreach (var sc in options.Cases)
        {
            if (swap.TryGetValue(sc.EventId, out var newEventId) && newEventId.HasValue)
            {
                sc.EventId = newEventId.Value;
                modified = true;
            }
        }

        // default 
        if (swap.TryGetValue(options.DefaultEventId, out var newDefaultEventId) && newDefaultEventId.HasValue)
        {
            options.DefaultEventId = newDefaultEventId.Value;
            modified = true;
        }

        if (!modified) return ValueTask.CompletedTask;

        genericActionOptions.Raw = GenericActionOptions.Convert<SwitchActionOptions, ExpandoObject>(options);

        return ValueTask.CompletedTask;
    }
}