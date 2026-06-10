using System;
using System.Collections.Generic;
using FluentAssertions;
using PI.ProductCatalog.Models;
using Xunit;

namespace UnitTests.ProductCatalog
{
    public class KeyDateTests
    {
        [Fact]
        public void Past()
        {
            var item = new CatalogItem
            {
                PendingDate = DateTime.UtcNow.AddDays(-1),
                EffectiveDate = DateTime.UtcNow.AddDays(-2),
            };

            item.KeyDates.IsEmpty().Should().BeTrue();
        }

        [Fact]
        public void Mix()
        {
            var date = DateTime.UtcNow.AddDays(1);
            var item = new CatalogItem
            {
                PromotionalStart = DateTime.UtcNow.AddDays(-1),
                PromotionalEnd = date,
            };

            item.KeyDates.Length.Should().Be(1);
            item.KeyDates[0].Should().Be(date.Date);
        }

        [Fact]
        public void Unique()
        {
            var date = DateTime.UtcNow.AddDays(1);
            var item = new CatalogItem
            {
                PromotionalStart = date,
                PromotionalEnd = date,
            };

            item.KeyDates.Length.Should().Be(1);
            item.KeyDates[0].Should().Be(date.Date);
        }

        [Fact]
        public void Multiple()
        {
            var date = DateTime.UtcNow.AddDays(1);
            var item = new CatalogItem
            {
                PromotionalStart = date,
                PromotionalEnd = date.AddDays(1),
                Costs = new[] {
                    new ItemCost
                    {
                        PromotionalStart = date,
                        PromotionalEnd = date.AddDays(2),
                        DroppedDate = date.AddDays(3)
                    },
                    new ItemCost
                    {
                        PromotionalStart = date.AddDays(-2),
                        PromotionalEnd = date.AddDays(1),
                        DroppedDate = date.AddDays(2)
                    }
                }
            };
            
            item.KeyDates.Length.Should().Be(6);
            item.KeyDates[0].Should().Be(date.Date);
            item.KeyDates[1].Should().Be(date.AddDays(1).Date);
            item.KeyDates[2].Should().Be(date.AddDays(2).Date);
            item.KeyDates[3].Should().Be(date.AddDays(3).Date);
            // dropped date adds check in the future
            item.KeyDates[4].Should().Be(date.AddDays(2).Add(CatalogItem.ElapsedBeforeRemoval).Date);
            item.KeyDates[5].Should().Be(date.AddDays(3).Add(CatalogItem.ElapsedBeforeRemoval).Date);
        }
    }
}
