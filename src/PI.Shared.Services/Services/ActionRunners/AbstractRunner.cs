using System;
using System.Dynamic;
using System.Threading.Tasks;
using Messages.Flow;
using PI.Shared.Exceptions;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Services.ActionRunners;

public abstract class AbstractRunner<T> : IActionRunner
    where T: ActionOptions
{
    public abstract Guid ActionId { get; }
    
    public ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, IActionOptions options)
    {
        if (options is not T typedOptions) {
            if (options is not GenericActionOptions genericActionOptions)
            {
                throw new BadRequestException("bad options");
            }

            typedOptions = genericActionOptions.ConvertTo<T>();
            typedOptions.Output = options.Output;
        }
        
        return RunAsync(context, typedOptions);
    }
    
    protected abstract ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, T options);

    protected bool TryGet(ActionRunnerContext context, ExpandoObject runContext, string path, out object outValue)
    {
        return ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, path, out  outValue);
    }

    protected bool TryGet<T1>(ActionRunnerContext context, ExpandoObject runContext, string path, out T1 outValue)
    {
        if (!TryGet(context, runContext, path, out var obj))
        {
            outValue = default;
            return false;
        }

        if (obj is T1 rightType)
        {
            outValue = rightType;
            return true;
        }

        outValue = default;
        return false;
    }
    
    protected bool TryGetGuid(ActionRunnerContext context, ExpandoObject runContext, string path, out Guid guid)
    {
        if (!TryGet(context, runContext, path, out var obj))
        {
            guid = default;
            return false;
        }

        if (obj is Guid uuid)
        {
            guid = uuid;
            return true;
        }

        if (obj is not string str)
        {
            guid = default;
            return false;
        }

        return Guid.TryParse(str, out guid);
    }
}