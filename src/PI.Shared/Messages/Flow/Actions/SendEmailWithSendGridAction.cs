using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace Messages.Flow;

public class SendEmailWithSendGridActionOptions : SimpleActionOptions
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TemplateSourceOptions
    {
        Unknown, 
        Inline, 
        SendGrid, 
        Unlayer,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum Recipient
    {
        Custom,
        Lead,
        AssignedEntity,
        Entity,
        Account,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum Sender
    {
        Entity,
        Account,
        System,
        Custom,
    }

    public TemplateSourceOptions TemplateSource { get; set; }
    
    /// <summary>
    /// Send grid template id
    /// Source == Sendgrid
    /// </summary>
    public string TemplateId { get; set; }
    
    /// <summary>
    /// Unlayer template id or {{path_to_property}}
    /// Source == Unlayer
    /// </summary>
    public string UnlayerTemplateId { get; set; }
    
    public Sender From { get; set; }
    public string FromName { get; set; }
    public string FromEmail { get; set; }
    public Recipient To { get; set; }
    public string ToName { get; set; }
    public string ToEmail { get; set; }
    public string ReplyToName { get; set; }
    public string ReplyToEmail { get; set; }
    
    /// <summary>
    /// Inline template for plain text
    /// Source == Inline
    /// </summary>
    public string PlainBody { get; set; }

    /// <summary>
    /// Inline template for html message
    /// Source == Inline
    /// </summary>
    public string HtmlBody { get; set; }
    
    public string BCC { get; set; }
    public string CC { get; set; }
    public string Subject { get; set; }

    /// <summary>
    /// Attachment object (look for content if not defined)
    /// </summary>
    public string AttachmentObjectType { get; set; }
    
    /// <summary>
    /// path to file object with .Content and .ContentType properties
    /// ...in the future could be smart and figure out that it is a file and go get it... 
    /// </summary>
    public string Attachment { get; set; }
    
    /// <summary>
    /// If an attachment is to be included, whether to inline it or not
    /// </summary>
    public bool InlineAttachment { get; set; }
}

public class SendEmailWithSendGridAction : FlowAction<SendLeadEmailWithSendGridActionOptions, SendLeadEmailWithSendGridAction.Message>
{
    public override Guid Id => ActionIds.SendEmailSendgrid;

    public class Message : SimpleActionMessage<SendEmailWithSendGridActionOptions>
    {
        public Message() { }

        public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
    }
}