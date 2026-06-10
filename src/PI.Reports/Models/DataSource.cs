using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.Dashboards;

public enum BeforeLoad
{
    DoNothing, 
    Truncate, 
    Drop,
}

[BsonCollection("bi.DataSource")]
[BsonDiscriminator(Required = true)]
[BsonKnownTypes(
    typeof(MongoDbDataSource),
    typeof(PostgresDataSource)
)]
public class DataSource : Model
{
    public string Group { get; set; }
    public string Description { get; set; }
    public TimeSpan? AutoRefreshInterval { get; set; }
    public DateTime? NextRefreshOn { get; set; }
    public DateTime? LastRefreshedOn { get; set; }
    public bool IsActive { get; set; } = true;
    public BeforeLoad BeforeLoad { get; set; }

    public LoadSource LoadSource { get; set; }
    
    public string[] Tags { get; set; }
}