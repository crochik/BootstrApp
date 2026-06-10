using FluentAssertions;
using PI.ProductCatalog.Models;
using Xunit;

namespace UnitTests.ProductCatalog
{
    public class UnitOfMeasurementTests
    {

        [Fact]
        public void RollLength()
        {
            var item = new CatalogItem
            {
                Material = MaterialClassification.New(MaterialType.Carpet),
                NominalLength = new Measurement
                {
                    Units = 12.06M,
                    UOM = UnitOfMeasurement.FeetAndInches,
                },
                ActualLength = new Measurement
                {
                    Units = 0,
                    UOM = UnitOfMeasurement.Feet
                },
                NominalWidth = null,
                ActualWidth = new Measurement
                {
                    Units = 12,
                    UOM = UnitOfMeasurement.FeetAndInches,
                }
            };

            item.RollLength.HasValue.Should().BeTrue();
            item.RollLength.Should().Be(12.5M);

            item.RollWidth.HasValue.Should().BeTrue();
            item.RollWidth.Should().Be(12);
        }

        [Fact]
        public void Equal()
        {
            var a = new Measurement
            {
                Units = 10,
                UOM = UnitOfMeasurement.Feet
            };

            var b = new Measurement
            {
                Units = 10,
                UOM = UnitOfMeasurement.Feet
            };

            a.Equals(b).Should().BeTrue();
        }
    }
}
