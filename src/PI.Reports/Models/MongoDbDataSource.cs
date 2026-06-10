using Crochik.Dipper;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Form.Models;

namespace PI.Shared.Models.Dashboards;

[BsonDiscriminator("mongodb")]
public class MongoDbDataSource : DataSource
{
    /// <summary>
    /// Use stored procedure (optional)
    /// If missing, requires object Type 
    /// </summary>
    public AggregateStoredProcedure StoredProcedure { get; set; }

    /// <summary>
    /// Data View
    /// </summary>
    public DataView DataView { get; set; }
}