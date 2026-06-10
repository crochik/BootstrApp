using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

[BsonDiscriminator("communication")]
public class CommunicationNote : Note // , IExternalId
{
    public const string OutboundSMS_ObjectType = "OutboundSMS";
    public const string InboundSMS_ObjectType = "InboundSMS";

    public const string CreatedStatus = "Created";
    public const string CompletedStatus = "Completed";
    public const string ConnectedStatus = "Connected";
    public const string CallRingingStatus = "Ringing";
    public const string UnansweredStatus = "Unanswered";
    public const string QueuedStatus = "Queued";
    public const string FailedStatus = "Failed";
    public const string SentStatus = "Sent";
    public const string DeliveredStatus = "Delivered";
    public const string UndeliveredStatus = "Undelivered";
    public const string ReceivedStatus = "Received";
    
    public string CommunicationChannel { get; set; }
    public CommunicationDirection Direction { get; set; }
    public CommunicationParty[] Parties { get; set; }
    
    public string Status { get; set; }
    public Dictionary<string, DateTime> Milestones { get; set; }
} 