using System;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models
{
    [BsonDiscriminator("verse")]
    public class VerseLeadIntegration : LeadIntegration, IExternalLeadIntegration
    {
        public DateTime? Received { get; set; }
        public DateTime? ReachedOut { get; set; }
        public DateTime? FirstResponse { get; set; }        
        public DateTime? Converted { get; set; }
        public DateTime? OptOut { get; set; }
        public DateTime? CancelledOn { get; set; }
    }
}