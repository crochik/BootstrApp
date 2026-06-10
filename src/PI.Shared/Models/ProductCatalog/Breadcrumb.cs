using System;
using System.Collections.Generic;
using System.Linq;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.ProductCatalog.Models
{
    public class BreadcrumbChildren
    {
        public string Type { get; set; }
        public int Count { get; set; }
    }

    public enum BreadcrumbType
    {
        Unspecified,
        Manufacturer,
        SfProduct,
        MaterialType,
        MaterialSubType,
        ProductType,
        Collection,
        Style
    }

    [BsonCollection("fcb2b.Breadcrumb")]
    [BsonDiscriminator(Required = true)]
    [BsonKnownTypes(
        typeof(CatalogManufacturer),
        typeof(CatalogSfProduct),
        typeof(CatalogStyle),
        typeof(CatalogMaterialType),
        typeof(CatalogMaterialSubType),
        typeof(CatalogProductType),
        typeof(CatalogCollection))
    ]
    public abstract class Breadcrumb : AbstractCatalogEntity
    {
        // [BsonElement("_t")]
        public abstract BreadcrumbType Type { get; }

        [BsonSerializer(typeof(ObjectIdAsGuidSerializer))]
        public Guid ParentId { get; set; }

        public int Count { get; set; }

        public BreadcrumbChildren[] Children { get; set; }

        private string _externalId;

        public string ExternalId
        {
            get => _externalId ?? Name;
            set => _externalId = value;
        }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Update count of children
        /// </summary>
        public void SetChildrenCount(string breadCrumbType, int count)
        {
            Children = (Children ?? Enumerable.Empty<BreadcrumbChildren>())
                .Where(x => x.Type != breadCrumbType)
                .Append(new BreadcrumbChildren
                {
                    Type = breadCrumbType,
                    Count = count
                })
                .Where(x => x.Count > 0)
                .ToArray();

            if (Children.Length == 0) Children = null;
        }

        // [BsonIgnore]
        // public string Type => (Attribute.GetCustomAttribute(GetType(), typeof(BsonDiscriminatorAttribute)) as BsonDiscriminatorAttribute)?.Discriminator;
    }

    [BsonDiscriminator("Manufacturer")]
    public class CatalogManufacturer : Breadcrumb
    {
        public override BreadcrumbType Type => BreadcrumbType.Manufacturer;
    }

    [BsonDiscriminator("SfProduct")]
    public class CatalogSfProduct : Breadcrumb
    {
        public override BreadcrumbType Type => BreadcrumbType.SfProduct;
    }

    public class PriceRange
    {
        public decimal Min { get; set; } = decimal.MaxValue;
        public decimal Max { get; set; } = decimal.MinValue;

        public void Append(decimal value) {
            if (Min>value) Min = value;
            if (Max<value) Max = value;
        }

        public string Format() => Math.Round(Min, 2) == Math.Round(Max, 2) ?
            $"${Min:.00}" :
            $"${Min:.00} - ${Max:.00}";

        public PriceRange WithMargin(decimal? margin)
            => !margin.HasValue ? null : new PriceRange
            {
                Min = CalculatePrice(Min, margin.Value),
                Max = CalculatePrice(Max, margin.Value)
            };

        private static decimal CalculatePrice(decimal cost, decimal margin) => Math.Round(100 * cost / (100 - margin), 2);
    }

    [BsonDiscriminator("Style")]
    public class CatalogStyle : Breadcrumb, ICustomProperties
    {
        public override BreadcrumbType Type => BreadcrumbType.Style;

        public Dictionary<string, object> Properties { get; set; }

        public MaterialClassification Material { get; set; }

        public string ObjectType => nameof(CatalogStyle);

        public Guid? ObjectStatusId { get; set; }

        public Guid? FlowId { get; set; }
        
        // public ObjectStatusMilestones ObjectStatusMilestones { get; set; }

        /// <summary>
        /// Conditional Cost Range (less expensive)
        /// Standard/Roll (rolls) or Pallet (non-rolls)
        /// </summary>
        public PriceRange ConditionalCostRange { get; set; }

        /// <summary>
        /// Standard Cost Range (more expensive)
        /// Cut (Roll) or "Standard" (non-rolls)
        /// </summary>
        public PriceRange StandardCostRange { get; set; }

        /// <summary>
        /// Range of all prices on all items 
        /// </summary>
        public PriceRange StandardPriceRange { get; set; }

        [BsonElement]
        public string CostRange => (StandardCostRange ?? ConditionalCostRange)?.Format();

        [BsonElement]
        public string PriceRange => StandardPriceRange?.Format();
    }

    [BsonDiscriminator("MaterialType")]
    public class CatalogMaterialType : Breadcrumb
    {
        public override BreadcrumbType Type => BreadcrumbType.MaterialType;
    }

    [BsonDiscriminator("MaterialSubType")]
    public class CatalogMaterialSubType : Breadcrumb
    {
        public override BreadcrumbType Type => BreadcrumbType.MaterialSubType;
    }

    [BsonDiscriminator("ProductType")]
    public class CatalogProductType : Breadcrumb
    {
        public override BreadcrumbType Type => BreadcrumbType.ProductType;
    }

    [BsonDiscriminator("Collection")]
    public class CatalogCollection : Breadcrumb
    {
        public override BreadcrumbType Type => BreadcrumbType.Collection;
    }
}