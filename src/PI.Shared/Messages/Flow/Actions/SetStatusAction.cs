using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class SetObjectStatusActionOptions : SimpleActionOptions, IActionOptionsForRunner
    {
        public Guid? ObjectStatusId { get; set; }
    }

    [Obsolete("just for transition to prevent deserialization exceptions")]
    public class SetLeadStatusActionOptions : SetObjectStatusActionOptions
    {
        public Guid? LeadStatusId
        {
            get => ObjectStatusId;
            set => ObjectStatusId = value;
        }
    }

    public class SetObjectStatusAction : FlowAction<SetObjectStatusActionOptions, SetObjectStatusAction.Message>
    {
        public override Guid Id => ActionIds.SetObjectStatus;

        public class Message : SimpleActionMessage<SetObjectStatusActionOptions>
        {
            public Message() { }
            public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
        }
    }
}