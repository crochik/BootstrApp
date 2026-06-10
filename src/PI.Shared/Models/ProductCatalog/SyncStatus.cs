using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.ProductCatalog.Models;

public class SyncStatus : LongTask
{
    [BsonSerializer(typeof(MagicGuidSerializer))]
    public Guid JobId { get; set; }

    public DateTime? InterchangeDate { get; set; }
    public int? TransactionControlNumber { get; set; }
    public FileInfo FileInfo { get; set; }

    public string Status { get; set; }
}