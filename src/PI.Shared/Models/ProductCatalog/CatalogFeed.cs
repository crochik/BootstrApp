using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.ProductCatalog.Models;

[BsonCollection("fcb2b.CatalogFeed")]
[BsonKnownTypes(typeof(B2BCatalogFeed), typeof(XLSCatalogFeed), typeof(CloneCatalogFeed), typeof(MALCatalogFeed))]
[BsonDiscriminator(Required = true)]
[UseObjectId]
public class CatalogFeed : FlowObjectModel, IExternalId
{
    public string ExternalId { get; set; }

    /// <summary>
    /// Process to update breadcrumbs
    /// </summary>
    public LongTask Breadcrumbs { get; set; }

    /// <summary>
    /// Last time was synced to Salesforce 
    /// </summary>
    public LongTask Salesforce { get; set; }

    /// <summary>
    /// Last change in one of its items
    /// </summary>
    public DateTime? LastUpdatedOn { get; set; }
}

[BsonDiscriminator("mal")]
public class MALCatalogFeed : CatalogFeed
{
}

[BsonDiscriminator("xls")]
public class XLSCatalogFeed : CatalogFeed
{
    /// <summary>
    /// Email Inbox to monitor for new files
    /// </summary>
    [BsonSerializer(typeof(MagicGuidSerializer))]
    public Guid EmailInboxId { get; set; }
}

[BsonDiscriminator("clone")]
public class CloneCatalogFeed : CatalogFeed
{
    /// <summary>
    /// Source of data
    /// </summary>
    [BsonSerializer(typeof(MagicGuidSerializer))]
    public Guid CatalogFeedId { get; set; }

    /// <summary>
    /// Last clone process
    /// </summary>
    public LongTask Clone { get; set; }
}

[BsonDiscriminator("b2b")]
public class B2BCatalogFeed : CatalogFeed
{
    public string SenderId { get; set; }
    public string ReceiverId { get; set; }
    public string Version { get; set; }
    public bool IsTest { get; set; }
    public string GroupSenderCode { get; set; }
    public string GroupReceiverCode { get; set; }
    public int? GroupControlNumber { get; set; }

    public Uri Url { get; set; }
    public string UserName { get; set; }

    // TODO: encrypt
    public string Password { get; set; }

    public SyncStatus LastSync { get; set; }
    public SyncStatus CurrentSync { get; set; }

    public DateTime? NextSyncDate { get; set; }
}