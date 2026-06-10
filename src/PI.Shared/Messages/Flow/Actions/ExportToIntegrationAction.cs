using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class ExportToIntegrationActionOptions : ActionOptions
    {
        public Guid? IntegrationId { get; set; }
        public UpdateOperation UpdateOperation { get; set; }

        public Guid? NextEventId { get; set; }
        public Guid? ErrorEventId { get; set; }
        
        public override ActionOutput[] Output { get; set; }
    }

    public class ExportToIntegrationAction : FlowAction<ExportToIntegrationActionOptions, ExportToIntegrationAction.Message>
    {
        public override Guid Id => ActionIds.ExportToIntegration;

        public class Message : SimpleActionMessage<ExportToIntegrationActionOptions>
        {
            public Message() { }
            public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
        }        
    }
}