using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Constants;

namespace Messages.Flow;

public class PostToGoogleChatActionOptions : SimpleActionOptions
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

public class PostToGoogleChatAction : FlowAction<PostToGoogleChatActionOptions, PostToGoogleChatAction.Message>
{
    public override Guid Id => ActionIds.PostToGoogleChat;
    public override string IconName => Id.ToString();

    public class Message : SimpleActionMessage<PostToGoogleChatActionOptions>
    {
        public Message() { }

        public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
    }
}