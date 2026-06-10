using System;
using Newtonsoft.Json;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class ExportToInspireNetActionOptions : SimpleActionOptions 
    {
    }

    public class ExportToInspireNetAction : FlowAction<ExportToInspireNetActionOptions, ExportToInspireNetAction.Message>
    {
        public override Guid Id => ActionIds.ExportLeadToInspireNet;

        public class Message : LeadWithApptActionMessage<ExportToInspireNetActionOptions>
        {   
            [JsonIgnore]
            public PI.Shared.Models.Lead Lead => Event.Lead.Lead;
        }        
    }
}