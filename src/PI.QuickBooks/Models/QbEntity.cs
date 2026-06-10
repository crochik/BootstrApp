using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.QuickBooks.Models;

[BsonCollection("quickbooks.Entity")]
public class QbEntity : EntityOwnedModel
{
    public string ExternalId { get; set; }
    public string FullyQualifiedName { get; set; }
    public string EntityType { get; set; }
    public IDictionary<string, object> Properties { get; set; }
    public Dictionary<string, object> Refs { get; set; }
}