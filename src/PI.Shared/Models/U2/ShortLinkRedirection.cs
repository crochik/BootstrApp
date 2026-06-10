using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.U2;

[BsonDiscriminator]
[DiscriminatorWithFallback]
[BsonCollection("u2.redirection")]
[BsonKnownTypes(typeof(ShareTemplate), typeof(ShareLink))]
public class ShortLinkRedirection : EntityOwnedModel
{
    public string Host { get; set; }
    public string ShortCode { get; set; }
    public string Location { get; set; }
    public int ViewCount { get; set; }
    public DateTime? LastAccessedOn { get; set; }
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether to redirect or proxy request (to hide url)
    /// NOT IMPLEMENTED YET
    /// </summary>
    public bool ProxyRequest { get; set; }
    
    public Dictionary<string, object> MetaValues { get; set; }
}

[BsonDiscriminator("template", Required = true)]
public class ShareTemplate : ShortLinkRedirection
{
}

[BsonDiscriminator("share", Required = true)]
public class ShareLink : ShortLinkRedirection
{
}