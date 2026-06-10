using System;
using Crochik.Mongo;
using PI.Shared.Models;

namespace Zapier.Models;

[BsonCollection("zapier.Subscription")]
public class Subscription : EntityOwnedModel
{
    /// <summary>
    /// Object Type subscribed to
    /// </summary>
    public string ObjectType { get; set; }
    
    /// <summary>
    /// Organization subscribed to
    /// </summary>
    public Guid? OrganizationId { get; set; }
    
    public string Url { get; set; }
    public string[] Keys { get; set; }
    public Guid ProfileId { get; set; }
    public string ClientId { get; set; }
}

// {
// "attempt": "018c1143-8afb-01dd-734f-f04c91d855bf",
// "id": "018c1143-8afb-01dd-734f-f04c91d855bf",
// "request_id": "018c1143-8afb-01dd-734f-f04c91d855bf",
// "status": "success"
// }