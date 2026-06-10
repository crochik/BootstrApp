using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

public enum ClipboardOperation
{
    Copy,
    Cut,
}

[BsonCollection("Clipboard")]
[BsonDiscriminator(Required = true)]
[BsonKnownTypes(typeof(FlowStepsClipboard))]
public class Clipboard : EntityOwnedModel
{
    public DateTime? ExpiresOn { get; set; }
    public ClipboardOperation Operation { get; set; }
    public bool IsActive { get; set; } = true;
    
    public bool IsShared { get; set; }
}

[BsonDiscriminator(nameof(FlowStep))]
public class FlowStepsClipboard : Clipboard
{
    /// <summary>
    /// Flow: Object Type 
    /// </summary>
    public string ObjectType { get; set; }
    
    /// <summary>
    /// Flow Id
    /// </summary>
    public Guid FlowId { get; set; }
    
    /// <summary>
    /// First step
    /// </summary>
    public Guid StepId { get; set; }
    
    /// <summary>
    /// Steps 
    /// </summary>
    public FlowStep[] Steps { get; set; }
}