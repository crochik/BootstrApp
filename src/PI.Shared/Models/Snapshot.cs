using System;
using System.Collections.Generic;
using Crochik.Dipper;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Form.Models;

namespace PI.Shared.Models;

[BsonCollection("Snapshot")]
[BsonKnownTypes(typeof(BulkEmail))]
[BsonDiscriminator(Required = true)]
[DiscriminatorWithFallback]
public class Snapshot : FlowObjectModel, IDataView
{
    /// <summary>
    /// Object Type that was the source of this snapshot
    /// </summary>
    public string SourceObjectType { get; set; }
    
    /// <summary>
    /// Collection where to save snapshot records 
    /// </summary>
    public string CollectionName { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public Guid CreatedById { get; set; }

    public DateTime? ExpiresOn { get; set; }

    public DateTime? Start { get; set; }

    public DateTime? End { get; set; }

    public Guid AppDataViewId { get; set; }

    public int? Count { get; set; }
    public string Error { get; set; }

    public AggregateStoredProcedure StoredProcedure { get; set; }
    public DataView DataView { get; set; }
    public DataViewOptions Options { get; set; }
}

[BsonCollection("SnapshotData")]
public class SnapshotData : EntityOwnedModel
{
    public Guid SnapshotId { get; set; }
    public string SnapshotObjectType { get; set; }
    public Dictionary<string, object> Properties { get; set; }
}