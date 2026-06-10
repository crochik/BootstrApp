using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.ProductCatalog.Models
{
    [BsonCollection("fcb2b.CatalogSyncJob")]
    public class CatalogSyncJob : FlowObjectModel
    {
        [BsonSerializer(typeof(MagicGuidSerializer))]
        public Guid CatalogFeedId { get; set; }

        public FileInfo FileInfo { get; set; }

        public DateTime? EndedOn { get; set; }
        public string Error { get; set; }

        public CatalogUpdate Interchange { get; set; }

        public int ItemsCount { get; set; }

        public string[] Log { get; set; }

        public string Url { get; set; }

        [BsonIgnore]
        public bool IsSuccess
            => EndedOn.HasValue &&
                string.IsNullOrEmpty(Error) &&
                (Interchange?.TransactionControlNumber.HasValue ?? false);
    }
}