using Crochik.Dipper;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.Dashboards;

[BsonDiscriminator("mongodb")]
public class MongoDbLoadSource : LoadSource
{
    /// <summary>
    /// Stored procedures to prepare (update data before it is exported)
    /// </summary>
    public AggregateStoredProcedure[] PrepareStoredProcedures { get; set; }

    /// <summary>
    /// Stored procedure to get DataView 
    /// </summary>
    public StoredProcedure StoredProcedure { get; set; }
}