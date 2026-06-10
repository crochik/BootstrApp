using System;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

[BsonDiscriminator("BulkEmail")]
public class BulkEmail : Snapshot
{
    public string ToNameField { get; set; }
    public string ToEmailField { get; set; }
    public string FromName { get; set; }
    public string FromEmail { get; set; }
    public string Subject { get; set; }
    public Guid? UnlayerTemplateId { get; set; }

    /// <summary>
    /// time zone to use
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// # of messages that were not generated because the e-mail couldn't be resolved 
    /// </summary>
    public int InvalidEmailCount { get; set; }

    public Guid? GenerationFlowRunId { get; set; }
    public DateTime? GenerationStartedOn { get; set; }
    public DateTime? GenerationEndedOn { get; set; }
    public int GeneratedCount { get; set; }

    /// <summary>
    /// Number of emails that failed to be generated
    /// (includes emails with invalid e-mail address)
    /// </summary>
    public int GenerateFailuresCount { get; set; }

    // stats
    public int QueuedCount { get; set; }
    public int QueuedFailuresCount { get; set; }

    public int DroppedCount { get; set; }
    public int OpenedCount { get; set; }
    public int DeliveredCount { get; set; }
    public int SpamReportCount { get; set; }
    public int BouncedCount { get; set; }
    // public int RepliedCount { get; set; }

    /// <summary>
    /// Do not try to queue until ...
    /// - when all the emails are generated it will get set based on the scheduled start
    /// - with each send will be (optionally) updated to include delay and/or reflect the set schedule
    /// </summary>
    public DateTime? DoNotQueueBefore { get; set; }

    /// <summary>
    /// Time the last email was queued (indicates that it is done sending)
    /// </summary>
    public DateTime? QueueFinishedOn { get; set; }

    /// <summary>
    /// The higher the number the higher the priority (goes first)
    /// </summary>
    public int Priority { get; set; }
}