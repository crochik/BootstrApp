using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Constants;

namespace Messages.Flow
{
    [Obsolete]
    public class PostLeadToSlackActionOptions : SimpleActionOptions
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Tos
        {
            AssignedUser,
            Entity,
            Other,
        }

        public Tos To { get; set; }
        public string Url { get; set; }
        public string Message { get; set; }
        public bool IncludeUrl { get; set; }
        public bool AuthenticatedUrl { get; set; }
    }

    [Obsolete]
    public class PostLeadToSlackAction : FlowAction<PostLeadToSlackActionOptions, PostLeadToSlackAction.Message>
    {
        public override Guid Id => ActionIds.PostLeadToSlackChannel;
        public override string IconName => Id.ToString();

        public class Message : LeadWithApptActionMessage<PostLeadToSlackActionOptions> { }
    }
}