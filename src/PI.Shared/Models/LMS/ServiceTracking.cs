using System;
using Crochik.Mongo;
using PI.Shared.Models;

namespace LMS.Models;

[BsonCollection("lms.ServiceTracking")]
public class ServiceTracking : Model
{
    public const string ObjectTypeName = "LMSServiceTracking";
    
    public string Description { get; set; }
    public Guid? EntityId { get; set; }

    public Guid? LeadTypeId { get; set; }
    public int Count { get; set; }
    public string[] Tags { get; set; }
    public string PostalCode { get; set; }
    public bool IsActive { get; set; } = true;
    
    public LeadConstraints Constraints { get; set; }
}

public enum BucketType
{
    Total,
    Day,
}

public class LeadConstraints
{
    public BucketType BucketType { get; set; }
    public string Key { get; set; }

    public int Count { get; set; }
    public int? MaxLeads { get; set; }
    
    public decimal Total { get; set; }
    public decimal? Budget { get; set; }
}