using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class SendLeadEmailWithMarketingCloudActionOptions : SimpleActionOptions
    {
    }

    public class SendLeadEmailWithMarketingCloudAction : FlowAction<SendLeadEmailWithMarketingCloudActionOptions, SendLeadEmailWithMarketingCloudAction.Message>
    {
        public override Guid Id => ActionIds.SendLeadEmailMarketingCloud;

        public class Message : LeadWithApptActionMessage<SendLeadEmailWithMarketingCloudActionOptions> { }
    }
}