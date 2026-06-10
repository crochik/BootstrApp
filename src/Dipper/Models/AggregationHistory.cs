using System;
using Crochik.Dipper;
using Crochik.Mongo;

namespace Models;

public class PipelineVersion
{
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public string Value { get; set; }
}

[BsonCollection("Dipper.History")]
public class AggregationHistory : PI.Shared.Models.AppElement
{
    public const string ObjectTypeFullName = "dipper.History";
    
    public Guid UserId { get; set; }
    public string Namespace { get; set; }
    public Parameter[] Parameters { get; set; }
    public string Collection { get; set; }
    public string[] Pipeline { get; set; }
    public PipelineVersion[] Versions { get; set; }
}