using System;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models
{
    public class O365Tenant
    {
        [BsonId]
        
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }

        public string Name { get; set; }

        
        public Guid? IdentityId { get; set; }
    }    
}