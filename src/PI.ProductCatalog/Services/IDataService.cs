using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Services;

public interface IDataService
{
    Task<List<CatalogItem>> GetItemsAsync(CatalogStyleOperation op, string styleNumber);
    Task<CatalogItem> GetItemAsync(CatalogUpdate update, string sku);
    void Add(CatalogItem item);
    void Add(CatalogOperation op);
    void Update(CatalogItemOperation op, CatalogItem existing, PropertyUpdate[] updates);
    Task AppendToLogAsync(Guid jobId, string[] message);
    Task FlushAsync(bool force);
}