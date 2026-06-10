using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    [Obsolete]
    public class ExportToLuminActionOptions : ActionOptions
    {
        public Guid? NextEventId { get; set; }
        public Guid? ErrorEventId { get; set; }
        public UpdateOperation UpdateOperation { get; set; }
        public override ActionOutput[] Output { get; set; }
    }

    [Obsolete]
    public class ExportToLuminAction : FlowAction<ExportToLuminActionOptions, ExportToLuminAction.Message>
    {
        public override Guid Id => ActionIds.ExportLeadToLumin;

        public class Message : SimpleActionMessage<ExportToLuminActionOptions>
        {
            public Message() { }
            public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
        }
    }
}