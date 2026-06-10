using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    [Obsolete]
    public class UpdateIntegrationForLeadActionOptions : SimpleActionOptions
    {
    }

    public class UpdateIntegrationForLeadAction : FlowAction<UpdateIntegrationForLeadActionOptions, UpdateIntegrationForLeadAction.Message>
    {
        public override Guid Id => ActionIds.UpdateIntegrationForLead;

        public class Message : LeadWithApptActionMessage<UpdateIntegrationForLeadActionOptions> { }
    }
}