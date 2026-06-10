using System;
using Crochik.Mongo;
using PI.Shared.Models.Interfaces;

namespace PI.Shared.Models.U2;

[BsonCollection("u2.SingleResourceAccessToken")]
public class SingleResourceAccessToken : EntityOwnedModel, IWithParent
{
    public const string ObjectTypeFullName = "u2.SingleResourceAccessToken";
    
    public int? MaxViewCount { get; set; }
    public DateTime? Expiration { get; set; }
    
    public int ViewCount { get; set; }
    public ReferencedObject Parent { get; set; }
    
    // public DateTime? LastAccessedOn { get; set; }
    public bool IsActive { get; set; }
}