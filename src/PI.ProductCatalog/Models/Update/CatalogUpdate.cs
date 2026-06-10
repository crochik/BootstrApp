using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.ProductCatalog.Models
{
    public enum CatalogOperationType
    {
        Unspecified,
        Delete,
        Update,
    }

    public class CatalogUpdate : FlowObjectModel
    {
        [BsonSerializer(typeof(MagicGuidSerializer))]
        public Guid CatalogFeedId { get; set; }
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public DateTime? InterchangeDate { get; set; }
        public string Version { get; set; }
        public int? ControlNumber { get; set; }
        public bool IsTest { get; set; }
        public string GroupSenderCode { get; set; }
        public string GroupReceiverCode { get; set; }
        public int GroupControlNumber { get; set; }
        public int? TransactionControlNumber { get; set; }
        public CatalogPricing Pricing { get; set; }
        public CatalogOperationType? OperationType { get; set; }
        public Currency Currency { get; set; }
        public string Vendor { get; set; }
        public string AccountNumber { get; set; }

        public CatalogUpdate() { }
    }
}