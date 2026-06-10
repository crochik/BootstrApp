using System;
using System.Threading.Tasks;
using Messages.Flow;
using PI.Shared.Models;

namespace PI.Shared.Services.ActionRunners;

public class ActionRunnerContext
{
    public Guid ObjectId { get; init; }
    public IEntityContext EntityContext { get; init; } 
    public ObjectType ObjectType { get; init; }
    public FlowRun Run { get; init; }
    public FlowEvent Event { get; init; }

    public ActionRunnerContext()
    {
    }
}

public interface IActionRunner
{
    public Guid ActionId { get; }
    public ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, IActionOptions options);
}