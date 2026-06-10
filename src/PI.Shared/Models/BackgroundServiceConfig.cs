#nullable enable
using System;
using System.Collections.Generic;
using Crochik.Mongo;

namespace PI.Shared.Models;

[BsonCollection("job.Service")]
public class BackgroundServiceConfig : Model, IFlowObject, IExternalId, ITaggable
{
    public string Description { get; set; }
    public string ObjectType => GetType().Name;

    public string ExternalId { get; set; }
    public string[] Tags { get; set; }

    public long? AvailableInstances { get; set; }
    public long? ConcurrentInstances { get; set; }
    public long? MaxConcurrentInstances { get; set; }

    public Guid? LastTransactionId { get; set; }
    public DateTime? StartedOn { get; set; }
    public DateTime? EndedOn { get; set; }
    public string LastError { get; set; }
    public Guid? ObjectStatusId { get; set; }
    public Guid? FlowId { get; set; }
    public bool IsActive { get; set;  }
    
    // public ObjectStatusMilestones ObjectStatusMilestones { get; set; }
}