using System;
using Messages.Flow;

namespace PI.Shared.Models;

public class FlowStep 
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventIdTrigger { get; set; }
    public Guid? CurrentStatusId { get; set; }
    public string Description { get; set; }
    public string IconName { get; set; }
    public Guid ActionId { get; set; }
    public ActionOptions Options { get; set; }

    public IFlowActionMessage Action
    {
        set
        {
            ActionId = value.Id;
            Options = value.ActionOptions;
            IconName = value.IconName;
        }
    }
}

// public class FlowTransition : IFlowTransition
// {
//     public Guid EntityId { get; set; }
//     public Guid TargetFlowId { get; set; }
//     public string Tag { get; set; }
//
//     [BsonIgnore]
//     public Guid CurrentFlowId { get; set; }
// }