using System;
using FluentAssertions;
using PI.ProductCatalog.Models;
using Xunit;

namespace UnitTests.ProductCatalog
{
    public class ItemCostTests
    {
        [Fact]
        public void Past()
        {
            var item = new CatalogItem
            {
                Material = new MaterialClassification
                {
                    Type = MaterialType.Carpet,
                    SubType = MaterialSubType.Indoor
                },
                Costs = new[]
                {
                    new ItemCost
                    {
                        Criteria = PriceCriteria.Promotional,
                        PromotionalStart = DateTime.UtcNow.AddDays(-5),
                        PromotionalEnd = DateTime.UtcNow.AddDays(-1),
                        UnitCost = 1,
                        PackageCondition = PackagePriceCondition.Cut,
                    },
                    new ItemCost
                    {
                        Criteria = PriceCriteria.List,
                        UnitCost = 2,
                        PackageCondition = PackagePriceCondition.Cut,
                    }
                }
            };

            item.Update();

            item.Costs.Length.Should().Be(1);
            item.CutCost.Criteria.Should().Be(PriceCriteria.List);
        }

        [Fact]
        public void Promo()
        {
            var item = new CatalogItem
            {
                Material = new MaterialClassification
                {
                    Type = MaterialType.Carpet,
                    SubType = MaterialSubType.Indoor
                },
                Costs = new[]
                {
                    new ItemCost
                    {
                        Criteria = PriceCriteria.Promotional,
                        PromotionalStart = DateTime.UtcNow.AddDays(-5),
                        PromotionalEnd = DateTime.UtcNow.AddDays(1),
                        UnitCost = 1,
                        PackageCondition = PackagePriceCondition.StandardRollLength,
                    },
                    new ItemCost
                    {
                        Criteria = PriceCriteria.Promotional,
                        PromotionalStart = DateTime.UtcNow.AddDays(3),
                        PromotionalEnd = DateTime.UtcNow.AddDays(5),
                        UnitCost = 2,
                        PackageCondition = PackagePriceCondition.StandardRollLength,
                    },
                    new ItemCost
                    {
                        Criteria = PriceCriteria.List,
                        UnitCost = 3,
                        PackageCondition = PackagePriceCondition.StandardRollLength,
                    }
                }
            };

            item.Update();

            item.Costs.Length.Should().Be(3);
            item.StandardCost.Criteria.Should().Be(PriceCriteria.Promotional);
            item.StandardCost.UnitCost.Should().Be(1);
        }

        [Fact]
        public void MoreExpensice()
        {
            var item = new CatalogItem
            {
                Material = new MaterialClassification
                {
                    Type = MaterialType.CeramicTile,
                    SubType = MaterialSubType.FloorTile
                },
                Costs = new[]
                {
                    new ItemCost
                    {
                        Criteria = PriceCriteria.Promotional,
                        PromotionalStart = DateTime.UtcNow.AddDays(-5),
                        PromotionalEnd = DateTime.UtcNow.AddDays(1),
                        UnitCost = 1,
                        PackageCondition = PackagePriceCondition.StandardRollLength,
                    },
                    new ItemCost
                    {
                        Criteria = PriceCriteria.Promotional,
                        PromotionalStart = DateTime.UtcNow.AddDays(-1),
                        PromotionalEnd = DateTime.UtcNow.AddDays(5),
                        UnitCost = 2,
                        PackageCondition = PackagePriceCondition.StandardRollLength,
                    },
                    new ItemCost
                    {
                        Criteria = PriceCriteria.List,
                        UnitCost = 3,
                        PackageCondition = PackagePriceCondition.StandardRollLength,
                    }
                }
            };

            item.Update();

            item.Costs.Length.Should().Be(3);
            item.StandardCost.Criteria.Should().Be(PriceCriteria.Promotional);
            item.StandardCost.UnitCost.Should().Be(2);
        }
    }
}
