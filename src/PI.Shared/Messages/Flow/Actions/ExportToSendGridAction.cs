using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class ExportToSendGridActionOptions : SimpleActionOptions
    {
    }

    public class ExportToSendGridAction : FlowAction<ExportToSendGridActionOptions, ExportToSendGridAction.Message>
    {
        public override Guid Id => ActionIds.ExportLeadToSendGrid;

        public class Message : LeadWithApptActionMessage<ExportToSendGridActionOptions> { }
    }
}