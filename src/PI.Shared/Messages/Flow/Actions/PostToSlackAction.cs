using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class PostToSlackActionOptions : SimpleActionOptions
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Tos
        {
            Custom,
            Account,
            System,
        }

        public Tos To { get; set; }
        public string Url { get; set; }
        public string Message { get; set; }
    }

    public class PostToSlackAction : FlowAction<PostToSlackActionOptions, PostToSlackAction.Message>
    {
        public override Guid Id => ActionIds.PostLeadToSlackChannel;
        public override string IconName => Id.ToString();

        public class Message : SimpleActionMessage<PostToSlackActionOptions>
        {
            public Message() { }

            public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
        }
    }
}