using System;
using System.Linq;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.ProductCatalog.Models;

namespace Controllers.Models
{
    public class CatalogItemView : IDescribableCatalogItem
    {
        [BsonId]
        [BsonSerializer(typeof(ObjectIdAsGuidSerializer))]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description => this.GetDescription();
        public string Manufacturer { get; set; }
        public string StyleNumber { get; set; }
        public MaterialClassification Material { get; set; }
        public string ProductType { get; set; }
        public string CollectionName { get; set; }
        public string StyleName { get; set; }
        public Measurement NominalLength { get; set; }
        public Measurement ActualLength { get; set; }
        public Measurement NominalWidth { get; set; }
        public Measurement ActualWidth { get; set; }

        [JsonIgnore]
        public Measurement SellingUnit { get; set; }

        [JsonProperty("sellingUnit")]
        public string SellingUnitStr => SellingUnit?.ToString();

        [JsonIgnore]
        public BaseUnit BaseUnit { get; set; }

        [JsonProperty("baseUnit")]
        public string BaseUnitStr => BaseUnit?.ToString();

        [JsonIgnore]
        public ItemCost[] Costs { get; set; }

        [JsonProperty("cost1")]
        public ItemCost Cost1 => IsRollGoods ? CutCost : StandardCost;

        [JsonProperty("cost2")]
        public ItemCost Cost2 => IsRollGoods ? StandardCost : PalletCost;

        [JsonIgnore]
        public ItemCost StandardCost { get; set; }

        [JsonIgnore]
        public ItemCost CutCost { get; set; }
        
        [JsonIgnore]
        public ItemCost PalletCost { get; set; }

        [JsonProperty("formattedCost1")]
        public string FormattedStandardCost => Cost1?.ToString();

        [JsonProperty("formattedCost2")]
        public string FormattedCutCost => Cost2?.ToString();

        [JsonProperty("formattedPrice1")]
        public string FormattedStandardPrice
        {
            get
            {
                var price = Cost1?.CalculatePrice(Margin);
                return price.HasValue ? $"${price:.##}" : null;
            }
        }

        [JsonProperty("formattedPrice2")]
        public string FormattedCutPrice
        {
            get
            {
                var price = Cost2?.CalculatePrice(Margin);
                return price.HasValue ? $"${price:.##}" : null;
            }
        }

        public decimal? Margin { get; set; }

        [JsonProperty("width")]
        public string Width => (NominalWidth ?? ActualWidth)?.ToString();

        [JsonProperty("length")]
        public string Length => (NominalLength ?? ActualLength)?.ToString();
        public UOMRate[] Packaging { get; set; }

        [JsonProperty("carton")]
        public string Carton => Packaging?.FirstOrDefault(x => x.UOM == UnitOfMeasurement.Carton)?.Measurement.ToString();

        [JsonProperty("pallet")]
        public string Pallet => Packaging?.FirstOrDefault(x => x.UOM == UnitOfMeasurement.Pallet)?.Measurement.ToString();

        public string SKU { get; set; }

        public string ColorName { get; set; }
        public string ColorNumber { get; set; }

        // added by aggregation
        public string MaterialType => Material?.Type.ToString();
        public string MaterialSubType => Material?.SubType.ToString();

        [JsonIgnore]
        public DateTime? DroppedDate { get; set; }

        [JsonIgnore]
        public DateTime? PromotionalStart { get; set; }

        [JsonIgnore]
        public DateTime? PromotionalEnd { get; set; }

        [JsonIgnore]
        public bool IsActive { get; set; }

        private bool IsRollGoods => Material?.IsRollGoods ?? false;
    }
}
