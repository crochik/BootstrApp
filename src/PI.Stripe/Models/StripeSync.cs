using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace Models
{
    public class StripeSyncResouce
    {
        public string LastSyncedId { get; set; }
        public DateTime? LastStart { get; set; }
        public DateTime? LastSuccessEnd { get; set; }
        public DateTime? LastError { get; set; }
    }

    // TODO: should have been an integration at the account level :(
    [BsonCollection("stripe.Sync")]
    public class StripeSync : Model
    {
        // Webpoints

        // Sync
        public StripeSyncResouce Customer { get; set; }
        public StripeSyncResouce Charges { get; set; }

        [BsonSerializer(typeof(EncryptedStringSerializer<StripeSync>))]
        public string ApiKey { get; set; }

        [BsonSerializer(typeof(EncryptedStringSerializer<StripeSync>))]
        public string EndpointSecret { get; set; }
    }
}

