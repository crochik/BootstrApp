using System;
using System.Linq;
using FluentAssertions;
using PI.ProductCatalog;
using PI.ProductCatalog.Models;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests.ProductCatalog;

public class MergerTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public MergerTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void DropDateTest()
    {
        var createdOn = DateTime.UtcNow;
        var existing = new CatalogItem
        {
            CreatedOn = createdOn,
            DroppedDate = DateTime.Now,
            PromotionalStart = DateTime.Now,
            PromotionalEnd = DateTime.Now,
            EffectiveDate = DateTime.Now,
            PendingDate = DateTime.Now,
        };

        var item = new CatalogItem
        {
            CreatedOn = createdOn,
            DroppedDate = null,
            PromotionalStart = null,
            PromotionalEnd = null,
            EffectiveDate = null,
            PendingDate = null,
        };
        
        var merger = Merger<CatalogItem, CatalogItem>.Merge(item, existing);
        merger.Result.DroppedDate.Should().BeNull();
        merger.Result.PromotionalStart.Should().BeNull();
        merger.Result.PromotionalEnd.Should().BeNull();
        merger.Result.EffectiveDate.Should().BeNull();
        merger.Result.PendingDate.Should().BeNull();

        // _testOutputHelper.WriteLine(string.Join(",", merger.Updates.Select(x=>x.Name) ));
        merger.Updates.Count.Should().Be(5);
    }
    
//         IMongoDatabase Database => new MongoClient().GetDatabase("test");
//
//         UpdateQuery<CatalogItem> UpdateQuery => new Query<CatalogItem>(Database)
//             .Eq(x => x.Id, Model.NewObjectId())
//             .Update
//                 .Set(x => x.LastModifiedOn, DateTime.UtcNow);
//
//         IMapper Mapper
//         {
//             get
//             {
//                 var config = new MapperConfiguration(cfg =>
//                 {
//                     cfg.AddMaps(typeof(App.Program).Assembly);
//                     cfg.AddMaps(typeof(PI.Shared.Models.Model).Assembly);
//                     cfg.AddMaps(typeof(CatalogItem).Assembly);
//
//                     cfg.AllowNullCollections = true;
//                 });
//
//                 config.AssertConfigurationIsValid();
//                 return config.CreateMapper();
//             }
//         }
//
//         CompareLogic CompareLogic
//         {
//             get
//             {
//                 var config = new ComparisonConfig
//                 {
//                     MaxDifferences = 100,
//                 };
//
//                 config.MembersToIgnore.Add($"{nameof(CatalogItem)}.{nameof(CatalogItem.Id)}");
//                 config.MembersToIgnore.Add($"{nameof(CatalogItem)}.{nameof(CatalogItem.CreatedOn)}");
//                 config.MembersToIgnore.Add($"{nameof(CatalogItem)}.{nameof(CatalogItem.LastModifiedOn)}");
//                 config.MembersToIgnore.Add($"{nameof(CatalogItem)}.{nameof(CatalogItem.LastActor)}");
//
//                 // config.IgnoreCollectionOrder = true;
//                 return new CompareLogic(config);
//             }
//         }
//
//         [Fact]
//         public void DetectChanges()
//         {
//             var update = new CatalogItemUpdate
//             {
//                 SKU = "SKU",
//                 ColorName = "Blue",
//                 ColorNumber = "BLU",
//                 StyleName = "Modern",
//                 ActualWidth = new Measurement
//                 {
//                     Units = 1,
//                     UOM = UnitOfMeasurement.SqFt
//                 },
//                 ActualLength = new Measurement
//                 {
//                     Units = 100,
//                     UOM = UnitOfMeasurement.SqFt
//                 },
//                 Material = MaterialClassification.Parse("CERFLOC"),
//                 EffectiveDate = DateTime.UtcNow,
//                 Packaging = new[]
//                 {
//                     new UOMRate
//                     {
//                         UOM = UnitOfMeasurement.Package,
//                         Measurement = new Measurement
//                         {
//                             Units =100,
//                             UOM = UnitOfMeasurement.SqFt
//                         }
//                     },
//                     new UOMRate
//                     {
//                         UOM = UnitOfMeasurement.Box,
//                         Measurement = new Measurement
//                         {
//                             Units =25,
//                             UOM = UnitOfMeasurement.SqFt
//                         }
//                     }
//                 },
//                 AssociatedSKUs = new[]
//                 {
//                     "test",
//                     "test2"
//                 }
//             };
//
//             var existing = new CatalogItem
//             {
//                 SKU = "SKU",
//                 ColorName = "Green",
//                 ColorNumber = "GRE",
//                 ActualWidth = new Measurement
//                 {
//                     Units = 1,
//                     UOM = UnitOfMeasurement.SqFt
//                 },
//                 ActualLength = new Measurement
//                 {
//                     Units = 99,
//                     UOM = UnitOfMeasurement.Feet
//                 },
//                 Material = MaterialClassification.Parse("CERFLO"),
//                 Packaging = new[]
//                 {
//                     new UOMRate
//                     {
//                         UOM = UnitOfMeasurement.Box,
//                         Measurement = new Measurement
//                         {
//                             Units =26,
//                             UOM = UnitOfMeasurement.Feet
//                         }
//                     },
//                     new UOMRate
//                     {
//                         UOM = UnitOfMeasurement.Package,
//                         Measurement = new Measurement
//                         {
//                             Units =100,
//                             UOM = UnitOfMeasurement.SqFt
//                         }
//                     }
//                 },
//                 AssociatedSKUs = new[]
//                 {
//                     "test",
//                     "test2"
//                 }
//             };
//
//             var merger = Execute(update, existing);
//
//             merger.Result.Should().NotBeNull();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.SKU)).Should().BeFalse();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.ColorName)).Should().BeTrue();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.ColorNumber)).Should().BeTrue();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.StyleName)).Should().BeTrue();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.ActualWidth)).Should().BeFalse();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.ActualLength)).Should().BeTrue();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.Material)).Should().BeTrue();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.EffectiveDate)).Should().BeTrue();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.Packaging)).Should().BeTrue();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.AssociatedSKUs)).Should().BeFalse();
//         }
//
//         [Fact]
//         public void Array()
//         {
//             var update = new CatalogItemUpdate
//             {
//                 Packaging = new[]
//                 {
//                     new UOMRate
//                     {
//                         UOM = UnitOfMeasurement.Package,
//                         Measurement = new Measurement
//                         {
//                             Units =100,
//                             UOM = UnitOfMeasurement.SqFt
//                         }
//                     },
//                     new UOMRate
//                     {
//                         UOM = UnitOfMeasurement.Box,
//                         Measurement = new Measurement
//                         {
//                             Units =25,
//                             UOM = UnitOfMeasurement.SqFt
//                         }
//                     }
//                 },
//                 AssociatedSKUs = new[]
//                 {
//                     "test",
//                     "test2",
//                     "test3"
//                 }
//             };
//
//             var existing = new CatalogItem
//             {
//                 Packaging = null,
//                 AssociatedSKUs = new[]
//                 {
//                     "test",
//                     "test2"
//                 }
//             };
//
//             var merger = Execute(update, existing);
//
//             merger.Result.Should().NotBeNull();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.Packaging)).Should().BeTrue();
//             merger.Updates.Exists(x => x.Name == nameof(CatalogItem.AssociatedSKUs)).Should().BeTrue();
//         }
//
//
//         private Merger<CatalogItem, CatalogItem> Execute(CatalogItemUpdate update, CatalogItem existing)
//         {
//             var mapped = Mapper.Map<CatalogItem>(update);
//             //var merger = Merger<CatalogItemUpdate, CatalogItem>.Merge(update, existing);
//             var merger = Merger<CatalogItem, CatalogItem>.Merge(mapped, existing);
//             
//             var query = UpdateQuery;
//             foreach (var prop in merger.Updates)
//             {
//                 System.Console.WriteLine($"{prop.Name}: from {prop.Previous ?? "[NULL]"} to {prop.After}");
//                 query.Set(prop.Name, prop.After);
//             }
//
//             var filterBson = Database.ToBsonDocument(query.Filter);
//             var updateBson = Database.ToBsonDocument(query.UpdateDefinition);
//             System.Console.WriteLine(filterBson);
//             System.Console.WriteLine(updateBson);
//
//             UsingMapperAndCompareLogic(update, existing);
//
//             return merger;
//         }
//
//         private void UsingMapperAndCompareLogic(CatalogItemUpdate update, CatalogItem existing)
//         {
//             var mapped = Mapper.Map<CatalogItem>(update);
//             var result = CompareLogic.Compare(mapped, existing);
//
//             System.Console.WriteLine(result.DifferencesString);
//         }
}