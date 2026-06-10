using System;
using Crochik.Mongo;

namespace PI.Shared.Models;

[Obsolete("use new remote items")]
[BsonCollection("RemoteFile")]
public class UnlayerRemoteFile : EntityOwnedModel
{
    public string ContentType { get; set; }
    public int Size { get; set; }
    public string Provider { get; set; }
    public string Uri { get; set; }
    public string PublicUrl { get; set; }
}