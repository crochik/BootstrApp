using System;
using PI.Shared.Constants;

namespace Messages.Flow;

[Obsolete]
public class AssignFlowActionOptions : SimpleActionOptions
{
    public Guid? FallbackFlowId { get; set; }
    public string Tag { get; set; }
}

[Obsolete]
public class AssignFlowAction : FlowAction<AssignFlowActionOptions, AssignFlowAction.Message>
{
    public override Guid Id => ActionIds.AssignFlow;

    public class Message : LeadWithApptActionMessage<AssignFlowActionOptions> { }
}