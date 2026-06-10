using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using PI.ProductCatalog;
using PI.ProductCatalog.Edi832;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Services;
using PI.Shared.Constants;
using PI.Shared.Models;
using Xunit;
using FileInfo = PI.ProductCatalog.Models.FileInfo;

namespace UnitTests.ProductCatalog;

public class ParserTest
{
    private IMapper GetMapper()
    {
        var config = new MapperConfiguration(cfg => { ConfigureMapper(cfg); });
        config.AssertConfigurationIsValid();
        return config.CreateMapper();

        void ConfigureMapper(IMapperConfigurationExpression cfg)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.FullName.StartsWith("PI.")) continue;
                if (GetType().Assembly == assembly) continue;
                cfg.AddMaps(assembly);
            }
        }
    }
    
    protected string GetFilePath(string fileName)
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        return Path.Combine(workingDirectory, "..", "..", "..", "ProductCatalog", "files", fileName);
    }    

    // ??????
    [Fact]
    public async Task ShawLastSLN()
    {
        var mapper = GetMapper();
        var ds = new FakeDataService();
        var loader = new Loader(new FakeLogger<Loader>(), mapper, ds);
        var parser = new CatalogParser(new FakeLogger<CatalogParser>(), loader);

        var context = new AccountContext(AccountIds.FCI);
        var sender = new ShawSender();
        var job = new CatalogSyncJob
        {
            FileInfo = new FileInfo
            {
                // Url = new UriBuilder(serverUrl) { Path = nextFile.Path }.Uri,
                // ModifiedDate = nextFile.Date,
                Filename = "191-slim.txt",
            },
            Interchange = new CatalogUpdate
            {
                Id = Model.NewObjectId(),
                AccountId = context.AccountId.Value,
                EntityId = context.AccountId.Value,
                LastActor = context.Actor(),
                CatalogFeedId = Guid.NewGuid(),
                SenderId = sender.SenderId,
                // ReceiverId = feed.ReceiverId,
            }
        };
        
        var path = GetFilePath(job.FileInfo.Filename);

        var parserContext = new CatalogParserContext(context, job, path, sender);
        await parser.ParseAsync(parserContext);

        job.Error.Should().BeNull();
        ds.Loaded.Count.Should().Be(10);
        
        // IT WILL CHANGE DEPENDING ON THE CURRENT DATE SINCE IT WILL IGNORE PRICES ELAPSED
        // ds.Loaded[0].Costs.Length.Should().Be(2);
    }
    
    private class FakeDataService : IDataService
    {
        public List<CatalogItem> Loaded { get; } = new();
        
        public Task<List<CatalogItem>> GetItemsAsync(CatalogStyleOperation op, string styleNumber)
        {
            return Task.FromResult(new List<CatalogItem>());
        }

        public Task<CatalogItem> GetItemAsync(CatalogUpdate update, string sku)
        {
            // always new
            return Task.FromResult<CatalogItem>(null);
        }

        public void Add(CatalogItem item)
        {
            Console.WriteLine("add item: " + item.SKU);
            Loaded.Add(item);
        }

        public void Add(CatalogOperation op)
        {
            Console.WriteLine("add operation: " + op.Operation + ", " + op.Costs?.Count);
        }

        public void Update(CatalogItemOperation op, CatalogItem existing, PropertyUpdate[] updates)
        {
            Console.WriteLine("Update existing item");
        }

        public Task AppendToLogAsync(Guid jobId, string[] message)
        {
            foreach (var msg in message)
            {
                Console.WriteLine(msg);
            }

            return Task.CompletedTask;
        }

        public Task FlushAsync(bool force)
        {
            return Task.CompletedTask;
        }
    }
}
