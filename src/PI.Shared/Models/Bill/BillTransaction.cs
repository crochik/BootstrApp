using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models.Billing
{
    public class Reference
    {
        [BsonElement(Model.IdFieldName)]
        public Guid Id { get; set; }
        public string Type { get; set; }
        public string Tag { get; set; }
    }

    [DiscriminatorWithFallback]
    [BsonDiscriminator(Required = true)]
    [BsonCollection("bill.Transaction")]
    [BsonKnownTypes(typeof(Invoice), typeof(Payment), typeof(Adjustment), typeof(Dispute))]
    public class BillTransaction : Model
    {
        public Guid? OrganizationId { get; set; }
        public Guid? EntityId { get; set; }
        public string ExternalId { get; set; }
        public string Description { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal? Total { get; set; }
        public DateTime ReferenceDate { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal? Balance { get; set; }

        public int Number { get; set; }
        public Reference[] Refs { get; set; }
    }

    public class Adjustment : BillTransaction
    {
        public Guid? TransactionId { get; set; }
        public Guid AdjustedByEntityId { get; set; }
        public string AdjustedBy { get; set; }

        public Adjustment() { }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DisputeResolution
    {
        Unknown,
        Approve,
        Reject
    }

    public class Dispute : BillTransaction
    {
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal DisputedValue { get; set; }
        public Guid TransactionId { get; set; }
        public Guid InitiatedByEntityId { get; set; }
        public string InitiatedBy { get; set; }
        public Guid? ResolvedByEntityId { get; set; }
        public string ResolvedBy { get; set; }
        public Guid? AdjustmentId { get; set; }
        public DateTime? ResolvedOn { get; set; }
        public DisputeResolution? Resolution { get; set; }
        public Dictionary<string, object> ExtraMetadata { get; set; }

        public Dispute() { }
    }

    public enum PaymentStatus
    {
        Unknown,
        Succeeded,
        Pending,
        Failed
    }

    public class Payment : BillTransaction
    {
        public string Source { get; set; }
        public string ExternalUrl { get; set; }
    }

    public class Invoice : BillTransaction
    {
        public Item[] Items { get; set; }

        /// <summary>
        /// based on BillableItem
        /// </summary>
        public class Item
        {
            [BsonElement(Model.IdFieldName)]
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }

            [BsonRepresentation(BsonType.Decimal128)]
            public decimal Value { get; set; }
        }
    }

    public static class BillTransactionExtensions
    {
        public static IEnumerable<KeyValuePair<string, object>> GetRefs(this BillTransaction transaction)
        {
            yield return new KeyValuePair<string, object>("TransactionId", transaction.Id.ToString());
            if (transaction.OrganizationId.HasValue) yield return new KeyValuePair<string, object>("EntityId", transaction.OrganizationId.ToString());
            if (transaction.EntityId.HasValue) yield return new KeyValuePair<string, object>("EntityId", transaction.EntityId.ToString());
            if (transaction.Refs != null)
            {
                foreach (var r in transaction.Refs)
                {
                    yield return new KeyValuePair<string, object>(string.IsNullOrEmpty(r.Tag) ? r.Type.ToString() : $"{r.Type}:{r.Tag}", r.Id.ToString());
                }
            }
        }

        public static IEnumerable<KeyValuePair<string, object>> GetRefs(this Dispute transaction)
        {
            foreach (var r in ((BillTransaction)transaction).GetRefs()) yield return r;

            yield return new KeyValuePair<string, object>("TransactionId", transaction.TransactionId.ToString());
            yield return new KeyValuePair<string, object>("EntityId", transaction.InitiatedByEntityId.ToString());
            if (transaction.ResolvedByEntityId.HasValue) yield return new KeyValuePair<string, object>("EntityId", transaction.ResolvedByEntityId.Value.ToString());
        }

        public static IEnumerable<KeyValuePair<string, object>> GetRefs(this Adjustment transaction)
        {
            foreach (var r in ((BillTransaction)transaction).GetRefs()) yield return r;

            if (transaction.TransactionId.HasValue) yield return new KeyValuePair<string, object>("TransactionId", transaction.TransactionId.Value.ToString());
            yield return new KeyValuePair<string, object>("EntityId", transaction.AdjustedByEntityId.ToString());
        }

        public static IEnumerable<KeyValuePair<string, object>> GetMeta(this BillTransaction transaction)
        {
            if (transaction.Total.HasValue) yield return new KeyValuePair<string, object>(nameof(BillTransaction.Total), transaction.Total.Value);
            yield return new KeyValuePair<string, object>(nameof(BillTransaction.ExternalId), transaction.ExternalId);
            if (transaction.Number > 0) yield return new KeyValuePair<string, object>("TransactionNumber", transaction.Number);
        }

        public static IEnumerable<KeyValuePair<string, object>> GetMeta(this Payment transaction)
        {
            foreach (var r in ((BillTransaction)transaction).GetMeta()) yield return r;
            yield return new KeyValuePair<string, object>(nameof(Payment.Source), transaction.Source);
        }

        public static IEnumerable<KeyValuePair<string, object>> GetMeta(this Dispute transaction)
        {
            foreach (var r in ((BillTransaction)transaction).GetMeta()) yield return r;

            yield return new KeyValuePair<string, object>(nameof(Dispute.InitiatedBy), transaction.InitiatedBy);
            yield return new KeyValuePair<string, object>(nameof(Dispute.DisputedValue), transaction.DisputedValue);

            if (transaction.Resolution.HasValue)
            {
                yield return new KeyValuePair<string, object>(nameof(Dispute.Resolution), transaction.Resolution.Value.ToString());
                yield return new KeyValuePair<string, object>(nameof(Dispute.ResolvedOn), transaction.ResolvedOn);
                yield return new KeyValuePair<string, object>(nameof(Dispute.ResolvedBy), transaction.ResolvedBy);
            }

            if (transaction.ExtraMetadata != null)
            {
                foreach (var meta in transaction.ExtraMetadata)
                {
                    yield return meta;
                }
            }
        }

        public static IEnumerable<KeyValuePair<string, object>> GetMeta(this Adjustment transaction)
        {
            foreach (var r in ((BillTransaction)transaction).GetMeta()) yield return r;

            yield return new KeyValuePair<string, object>(nameof(Adjustment.AdjustedBy), transaction.AdjustedBy);
        }
    }
}