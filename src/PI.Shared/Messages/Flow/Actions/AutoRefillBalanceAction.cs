using System;
using PI.Shared.Constants;

namespace Messages.Flow;

public class AutoRefillBalanceActionOptions : ActionOptions
{
    public Guid? RefilledEventId { get; set; }
    public Guid? DisabledEventId { get; set; }
    public Guid? ErrorEventId { get; set; }

    public override ActionOutput[] Output { get; set; }
}

public class AutoRefillBalanceAction : FlowAction<AutoRefillBalanceActionOptions, AutoRefillBalanceAction.Message>
{
    public override Guid Id => ActionIds.AutoRefillBalance;
    public override string IconName => null;

    public class Message : SimpleActionMessage<AutoRefillBalanceActionOptions>
    {
        public Message() { }
        public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
    }
}