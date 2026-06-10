using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.ProductCatalog.Models;

namespace Controllers.Models
{
    public class BreadcrumbTree // : Breadcrumb
    {
        [BsonElement("_t")]
        public string Type { get; set; }

        [BsonId]
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Guid EntityId { get; set; }

        public Guid CatalogFeedId { get; set; }

        public string ExternalId { get; set; }

        public string Name { get; set; }

        public Guid ParentId { get; set; }
        public Guid[] ParentIds { get; set; }

        public decimal? Margin { get; set; }
        public bool? IsHidden { get; set; }
        public bool? IsActive { get; set; }
        public string[] Tags { get; set; }

        [BsonIgnore]
        public bool IsFavorite
        {
            get => Tags.Contains(AbstractCatalogEntity.FAVORITE_TAG);
            set => Tags.Set(AbstractCatalogEntity.FAVORITE_TAG, value);
        }

        // public BreadcrumbView[] ParentBreadcrumbs { get; set; }

        // public BreadcrumbChildren[] Children { get; set; }

        [BsonIgnore]
        [JsonIgnore]
        public Dictionary<object, BreadcrumbTree> ChildrenDict { get; set; } = new Dictionary<object, BreadcrumbTree>();

        public int Count { get; set; }

        [BsonIgnore]
        [JsonProperty("childrenNodes")]
        public IEnumerable<BreadcrumbTree> ChildrenNodes => ChildrenDict.Count > 0 ? ChildrenDict.Values.OrderBy(x => x.Name) : null;
    }
}
