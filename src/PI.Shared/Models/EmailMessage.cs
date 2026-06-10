using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

public class EmailAddress
{
    public string Name { get; set; }

    [BsonIgnore] [JsonIgnore] private readonly string _email;

    public string Email
    {
        get => _email;
        init => _email = value?.ToLowerInvariant();
    }
}

public class MimeContent
{
    public string ContentType { get; set; }
    public int Size { get; set; }
    public string Content { get; set; }
    public string Filename { get; set; }
}

public class Attachment : MimeContent
{
    public string ContentId { get; set; }
    public bool? Inline { get; set; }
}

public class EmailMessage
{
    public EmailAddress From { get; set; }
    public EmailAddress[] To { get; set; }
    public EmailAddress[] CC { get; set; }
    public EmailAddress[] BCC { get; set; }
    public EmailAddress ReplyTo { get; set; }

    public string Subject { get; set; }
    public string PlainBody { get; set; }
    public string HtmlBody { get; set; }

    public MimeContent[] Contents { get; set; }

    public string TemplateId { get; set; }

    [BsonIgnore] public object TemplateData { get; set; }
}

[BsonCollection("sendgrid.Email")]
public class SendGridEmailMessage : FlowObjectModel
{
    public Guid FlowRunId { get; set; }
    public string TriggerObjectType { get; set; }
    public Guid TriggerObjectId { get; set; }
    public EmailMessage Message { get; set; }
    public DateTime? Queued { get; set; }
    public DateTime? Opened { get; set; }
    public bool? OpenedByMachine { get; set; }
    public string OpenedIpAddress { get; set; }
    public string OpenedContentType { get; set; }
    public string OpenedByUserAgent { get; set; }
    public DateTime? Delivered { get; set; }
    public string DeliveredResponse { get; set; }
    public DateTime? Dropped { get; set; }
    public string DroppedReason { get; set; }
    
    public DateTime? SpamReported { get; set; }
    
    public DateTime? Bounced { get; set; }
    public string BounceType { get; set; }
    public string BounceReason { get; set; }
    public string BounceStatus { get; set; }

    public DateTime? Unsubscribed { get; set; }
    
    public string Error { get; set; }
    public List<KeyValuePair<string, object>> Refs { get; set; }
}

[BsonCollection("sendgrid.Event")]
public class SendGridEmailEvent
{
    [BsonId] 
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalId { get; set; }
    public Guid AccountId { get; set; }
    
    public Guid SendGridEmailMessageId { get; set; }
    public string Email { get; set; }
    public object Event { get; set; }
}

[BsonCollection("sendgrid.Unsubscribe")]
public class SendGridEmailUnsubscribe
{
    [BsonId]
    public string Email { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastModifiedOn { get; set; }
    public Dictionary<string, DateTime> SendGridEmailMessage { get; set; }
    public int Count { get; set; }
}