using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Constants;

namespace Messages.Flow;

public class SendSMSActionOptions : SimpleActionOptions
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Tos
    {
        Custom,
        
        /// <summary>
        /// Will infer the recipient from the object type
        /// currently only supports Lead and Appointment 
        /// </summary>
        Contact,
    }

    public Tos To { get; set; }
    
    /// <summary>
    /// expression to evaluate the target entityId
    /// only when custom 
    /// </summary>
    public string Entity { get; set; }
    
    /// <summary>
    /// (optional) expression to evaluate the target phone number
    /// only when custom 
    /// </summary>
    public string PhoneNumber { get; set; }
    
    public string Message { get; set; }
}

public class SendSMSAction : FlowAction<SendSMSActionOptions, SendSMSAction.Message>
{
    public override Guid Id => ActionIds.SendSMS;
    public override string IconName => Id.ToString();

    public class Message : SimpleActionMessage<SendSMSActionOptions>
    {
        public Message() { }

        public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
    }
}