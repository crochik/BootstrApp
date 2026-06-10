using System;
using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.Billing
{
    [BsonCollection("bill.Entity")]
    public class BillEntity : Model
    {
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Balance { get; set; }
        public int TransactionNumber { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal? PendingTotal { get; set; }

        public string[] PendingTransactions { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal? MinBalance { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal? MaxBalance { get; set; }
        public bool AutoRefill { get; set; }

        public DateTime? LastChargeOn { get; set; }

        public DateTime? LastFailedAttemptOn { get; set; }
    }
}