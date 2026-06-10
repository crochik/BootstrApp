using System;
using System.Collections.Generic;
using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.DataService;
using PI.ProductCatalog.Models;

namespace PI.QuickBooks.Services;

public class LocalCache
{
    public Dictionary<string, string> AccountIds { get; set; }
    public Dictionary<Guid, string> CatalogFeedIds { get; set; }
    public Dictionary<Guid, string> ItemIds { get; set; }
    public ServiceContext ServiceContext { get; set; }
    public DataService DataService { get; set; }

    public LocalCache()
    {
        
    }
    
    public bool TryGetIncomeAccountId(CatalogItem item, out string accountId)
        => TryGetIncomeAccountId(item.Material, out accountId);

    public bool TryGetExpenseAccountId(CatalogItem item, out string accountId)
        => TryGetExpenseAccountId(item.Material, out accountId);

    private bool TryGetIncomeAccountId(MaterialClassification materialClassification, out string accountId)
    {
        var incomeAccountName = materialClassification.Type switch
        {
            MaterialType.Accessories => "4070", // "Sales:Sales - Other",
            MaterialType.Installation or MaterialType.Labor => "4090", //"Sales:Installation Labor",
            MaterialType.Carpet => "4020", // "Sales:Carpet Sales",
            MaterialType.NaturalStones or MaterialType.CeramicTile => "4030", // "Sales:Ceramic and Stone Sales",
            MaterialType.Wood => "4040", // "Sales:Hardwood Sales",
            MaterialType.Laminates => "4050", // "Sales:Laminate Sales",
            MaterialType.Vinyl => "4060", // "Sales:Vinyl Sales",
            MaterialType.AreaRugs => "4010", // "Sales:Area Rug Sales",
            _ => "4070", // "Sales:Sales - Other",
        };

        return AccountIds.TryGetValue(incomeAccountName, out accountId);
    }
    
    private bool TryGetExpenseAccountId(MaterialClassification materialClassification, out string accountId)
    {
        var incomeAccountName = materialClassification.Type switch
        {
            MaterialType.Accessories => "5170", // "Material Costs - Other",
            MaterialType.Installation or MaterialType.Labor => "5200", //"Installation Costs",
            MaterialType.Carpet => "5120", // "Carpet Material",
            MaterialType.NaturalStones or MaterialType.CeramicTile => "5130", // "Ceramic and Stone Material",
            MaterialType.Wood => "5140", // "Hardwood Material",
            MaterialType.Laminates => "5150", // "Laminate Material
            MaterialType.Vinyl => "5160", // "Vinyl Material
            MaterialType.AreaRugs => "5110", // "Area Rugs
            _ => "5170", // "Material Costs - Other",
        };

        return AccountIds.TryGetValue(incomeAccountName, out accountId);
    }

    public bool TryGetItemType(CatalogItem item, out ItemTypeEnum type)
    {
        type = item.Material.Type switch
        {
            MaterialType.Installation or MaterialType.Labor => ItemTypeEnum.Service,
            _ => ItemTypeEnum.NonInventory,
        };

        return true;
    }

    public bool TryGetVendorId(CatalogFeed feed, out string vendorId)
        => TryGetVendorId(feed.Id, out vendorId);

    public bool TryGetVendorId(Guid catalogFeedId, out string vendorId)
    {
        if (CatalogFeedIds == null)
        {
            vendorId = default;
            return false;
        }

        return CatalogFeedIds.TryGetValue(catalogFeedId, out vendorId);
    }
}