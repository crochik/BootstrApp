using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Messages.Flow;
using PI.Shared.Models;
using PI.Shared.Services;

namespace FlowActions;

public class GenericActionBuilder : AbstractGenericActionBuilder
{
    private readonly GenericAction _action;

    public override Guid Id => _action.ActionId;
    public override string Name => _action.Name;
    public override string IconName => _action.IconName;
    public override string Description => _action.Description;
    public override string[] InputObjectTypes => _action.InputObjectTypes;
    protected override string ActionOptionsObjectType => _action.ActionOptionsObjectType;

    public GenericActionBuilder(GenericAction action, ObjectTypeService objectTypeService) : base(objectTypeService)
    {
        _action = action;
    }

    public override bool IsValidTrigger(string objectType)
    {
        return _action.InputObjectTypes == null || _action.InputObjectTypes.Length == 0 || _action.InputObjectTypes.Any(t => string.Equals(t, objectType));
    }

    protected override GenericActionOptions CalculateGenericOptions(ExpandoObject raw, GenericActionOptions current = null)
    {
        if (current?.Output != null)
        {
            // don't change outputs after creation
            return new GenericActionOptions
            {
                Raw = raw,
                Output = current.Output,
            };
        }

        var outputs = _action.Outputs?.Select(x => new ActionOutput
            {
                EventId = Guid.NewGuid(),
                Name = x.Key,
                Description = x.Value,
            })
            .ToArray();

        return new GenericActionOptions
        {
            Raw = raw,
            Output = outputs ?? [],
        };
    }

    public override ValueTask SwapAsync(IEntityContext context, FlowStep step, Dictionary<Guid, Guid?> swap)
    {
        // nothing else to do ?
        // the event ids are only stored in the output 
        return ValueTask.CompletedTask;
    }
}