using System;
using System.Threading;
using Crochik.Mongo;
using JsonSubTypes;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Models
{
    [DiscriminatorWithFallback]
    [BsonDiscriminator(Required = true)]
    [BsonKnownTypes(typeof(PartnerActor), typeof(APIActor), typeof(SingerSyncActor), typeof(JobActor))]
    [JsonConverter(typeof(JsonSubtypes), "_t")]
    public class Actor
    {
        [JsonProperty("_t")]
        [BsonIgnore]
        public virtual string _t => GetType().Name;

        private static AsyncLocal<Actor> _current { get; } = new AsyncLocal<Actor>();
        public static Actor Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }
    }

    public class AbstractAPIActor : Actor
    {
        public Guid AccountId { get; set; }
        public string ClientId { get; set; }
        public string TokenId { get; set; }
        public string RequestId { get; set; }
        public Guid? UserId { get; set; }

        protected AbstractAPIActor(
            Guid accountId,
            string clientId,
            string tokenId,
            string requestId,
            Guid? userId = null
            )
        {
            AccountId = accountId;
            ClientId = clientId;
            TokenId = tokenId;
            RequestId = requestId;
            UserId = userId;
        }

        protected AbstractAPIActor() { }
    }

    public class PartnerActor : AbstractAPIActor
    {
        public PartnerActor(
            Guid accountId,
            string clientId,
            string tokenId,
            string requestId,
            Guid? userId = null
            ) : base(accountId, clientId, tokenId, requestId, userId)
        {
        }

        public PartnerActor()
        {
        }
    }

    public class APIActor : AbstractAPIActor
    {
        public APIActor()
        {
        }

        public APIActor(
            IEntityContext context,
            string clientId,
            string tokenId,
            string requestId
            ) : base(context.AccountId.Value, clientId, tokenId, requestId)
        {
            UserId = context.UserId.Value;
        }

        public APIActor(
            Guid accountId,
            Guid userId,
            string clientId,
            string tokenId,
            string requestId
            ) : base(accountId, clientId, tokenId, requestId, userId)
        {
        }
    }

    [BsonDiscriminator("job")]
    public class JobActor : Actor
    {
        public Guid ServiceId { get; set; }
        public string TransactionId { get; set; }
    }

    public class SingerSyncActor : Actor
    {
        public Guid JobId { get; set; }

        public SingerSyncActor()
        {
        }

        [JsonConstructor]
        public SingerSyncActor(Guid jobId)
        {
            JobId = jobId;
        }
    }

    public class CatalogUpdateActor : Actor
    {
        [BsonId]
        [BsonSerializer(typeof(MagicGuidSerializer))]
        public Guid Id { get; set; }

        [JsonConstructor]
        public CatalogUpdateActor(Guid jobId)
        {
            Id = jobId;
        }
    }
}