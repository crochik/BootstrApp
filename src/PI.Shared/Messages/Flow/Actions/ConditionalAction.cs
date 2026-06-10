using System;
using PI.Shared.Constants;

namespace Messages.Flow;

public class ConditionalAction : FlowAction<ConditionalActionOptions, ConditionalAction.Message>
{
    public override Guid Id => ActionIds.Conditional;

    public class Message : SimpleActionMessage<ConditionalActionOptions>
    {
        public Message() { }

        public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
    }
}