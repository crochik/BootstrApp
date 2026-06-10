using System;
using Newtonsoft.Json;
using PI.Shared.Constants;

namespace Messages.Flow;

public class AssignActionOptions : SimpleActionOptions
{
    public Guid? EntityId { get; set; }
}

public class AssignAction : FlowAction<AssignActionOptions, AssignAction.Message>
{
    public override Guid Id => ActionIds.AssignLead;

    public class Message : LeadWithApptActionMessage<AssignActionOptions>
    {
        [JsonIgnore]
        public Guid? ActorId => Event.Context.EntityId;

        [JsonIgnore]
        public Guid? CurrentEntityId => Event.Lead.Lead.AssignedEntityId;

        [JsonIgnore]
        public Guid LeadId => Event.Lead.Lead.Id;

        [JsonIgnore]
        public Guid? TargetEntityId => Options.EntityId;
    }
}