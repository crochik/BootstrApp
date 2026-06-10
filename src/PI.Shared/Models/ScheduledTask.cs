using System;
using Crochik.Mongo;
using Messages.Flow;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

[BsonCollection("job.ScheduledTask")]
[DiscriminatorWithFallback]
[BsonDiscriminator(Required = true)]
[BsonKnownTypes(typeof(PostEventScheduledTask))]
public class ScheduledTask : Model
{
    public string Tag { get; set; }
    public DateTime Time { get; set; }
    public DateTime? Started { get; set; }
    public DateTime? Finished { get; set; }
    public string Error { get; set; }
        
    /// <summary>
    /// CronTab scheduler
    /// When set, it will automatically schedule a new task, after the conclusion
    /// </summary>
    public string AutoReschedule { get; set; }
}

[BsonDiscriminator("postEvent")]
public class PostEventScheduledTask : ScheduledTask
{
    public GenericFlowEvent Event { get; set; }
}

[BsonCollection("job.DataExtract")]
public class DataExtractJob : Model
{
    
}