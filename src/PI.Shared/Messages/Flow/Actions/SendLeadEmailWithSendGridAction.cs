using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class SendLeadEmailWithSendGridActionOptions : SimpleActionOptions
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Tos
        {
            AssignedUser,
            Lead,
            Other,
        }

        public string TemplateId { get; set; }
        public string FromName { get; set; }
        public string FromEmail { get; set; }
        public Tos To { get; set; }
        public string ToName { get; set; }
        public string ToEmail { get; set; }
        public string PlainBody { get; set; }
        public string HtmlBody { get; set; }
    }

    public class SendLeadEmailWithSendGridAction : FlowAction<SendLeadEmailWithSendGridActionOptions, SendLeadEmailWithSendGridAction.Message>
    {
        public override Guid Id => ActionIds.SendLeadEmailSendgrid;

        public class Message : LeadWithApptActionMessage<SendLeadEmailWithSendGridActionOptions> { }
    }
}