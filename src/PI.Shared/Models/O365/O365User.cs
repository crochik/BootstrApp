using System;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models
{
    public class O365User
    {
        [BsonId]
        public string Id { get; set; }
        public Guid IdentityId { get; set; }
        public string Name { get; set; }
        public Guid? TenantId { get; set; }

        public O365Subscription[] Subscriptions { get; set; }
    }    
}