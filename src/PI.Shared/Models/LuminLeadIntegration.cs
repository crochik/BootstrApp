using System;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models
{
    [BsonDiscriminator("lumin")]
    public class LuminLeadIntegration : LeadIntegration, IExternalLeadIntegration
    {
        /// <summary>
        /// Lumin Event
        /// </summary>
        public DateTime? ReachedOut { get; set; }

        /// <summary>
        /// Lumin Event
        /// </summary>
        public DateTime? FirstResponse { get; set; }

        /// <summary>
        /// Lumin Event
        /// </summary>
        public DateTime? Ineligible { get; set; }

        /// <summary>
        /// Lumin Event
        /// </summary>
        public DateTime? OptOut { get; set; }

        /// <summary>
        /// Lumin Event
        /// </summary>
        public DateTime? RequestCSR { get; set; }

        /// <summary>
        /// Lumin Event
        /// </summary>
        public DateTime? Converted { get; set; }

        /// <summary>
        /// Requested to be opted out
        /// </summary>
        public DateTime? CancelledOn { get; set; }
    }
}