using System;
using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Models
{
    /// <summary>
    /// Generic/base class to store data from integrations
    /// DANGER: adding a new type will requie every microservice that loads leads to be republished
    /// TODO: add custom deserializer?
    /// ...
    /// </summary>
    [DiscriminatorWithFallback]
    [BsonDiscriminator(Required = true)]
    [BsonKnownTypes(
        typeof(LuminLeadIntegration),
        typeof(VerseLeadIntegration)
    )]
    public class LeadIntegration : IIntegrationLead
    {
        public Guid IntegrationId { get; set; }
        public string ExternalId { get; set; }
        public string Status { get; set; }
        public string Url { get; set; }

        [BsonIgnore]
        public object Data { get; set; }

        [Obsolete]
        [BsonIgnore]
        public Guid LeadId { get; set; }

        [BsonElement("Data")]
        public BsonDocument SerializedData
        {
            get => Data == null ? null :
                (Data is BsonDocument doc) ? doc :
                BsonDocument.Parse(JsonConvert.SerializeObject(Data));

            set => Data = value == null ? null : BsonSerializer.Deserialize<BsonDocument>(value);
        }

        public string Tag { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime? LastModifiedOn { get; set; }

        public DateTime GetLastModified() => LastModifiedOn ?? CreatedOn;
    }

    public interface IExternalLeadIntegration : IIntegrationLead 
    {
        DateTime? ReachedOut { get; set; }

        DateTime? FirstResponse { get; set; }

        /// <summary>
        /// Lead was converted by integration
        /// </summary>
        DateTime? Converted { get; set; }

        DateTime? OptOut { get; set; }

        /// <summary>
        /// Integration was cancelled by PI
        /// </summary>
        DateTime? CancelledOn { get; set; }
    }
}