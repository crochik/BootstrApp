using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class WeightLoadBalanceActionOptions : SimpleActionOptions
    {
        
    }

    public class WeightLoadBalanceAction : FlowAction<WeightLoadBalanceActionOptions, WeightLoadBalanceAction.Message>
    {
        public override Guid Id => ActionIds.WeightLoadBalance;

        public class Message : LeadWithApptActionMessage<WeightLoadBalanceActionOptions>
        {

        }
    }
}