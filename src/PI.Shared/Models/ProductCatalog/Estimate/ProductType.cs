using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Models;

/// <summary>
/// Material Type for the main product (will define the entire selection for the room)
/// </summary>
// TODO: Have to figure out how to map from MaterialType/SubType to it...
public enum ProductType
{
    Hardwood,
    SandAndFinish,
    Laminate,
    SheetVinyl,
    Carpet,
    LuxuryVinyl,
    Tile,
    None,
}

public static class ProductTypeResolver
{
    public static bool TryResolve(CatalogItem item, out ProductType? productType)
        => TryResolve(item.Material, out productType);

    public static bool TryResolve(MaterialClassification material, out ProductType? productType)
        => TryResolve(material.Type, material.SubType, out productType);

    /// <summary>
    /// Resolve product type from material type
    /// there is a copy of this code in flutter 
    /// </summary>
    public static bool TryResolve(MaterialType materialType, MaterialSubType? subType, out ProductType? productType)
    {
        productType = materialType switch
        {
            MaterialType.Carpet => ProductType.Carpet,
            MaterialType.Wood => ProductType.Hardwood,
            MaterialType.Laminates => ProductType.Laminate,
            MaterialType.CeramicTile => ProductType.Tile,
            MaterialType.NaturalStones => ProductType.Tile,
            MaterialType.Vinyl => subType switch
            {
                MaterialSubType.VinylSheet => ProductType.SheetVinyl,
                MaterialSubType.VinylTile => ProductType.LuxuryVinyl,
                _ => null,
            },
            _ => null
        };

        return productType != null;
    }
}