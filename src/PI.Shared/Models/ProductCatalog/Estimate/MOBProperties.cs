namespace PI.ProductCatalog.Models;

public class MOBProperties
{
    public MaterialType MaterialType { get; set; }
    public MaterialSubType MaterialSubType { get; set; }
    public string SfExternalId { get; set; }
    public string SfProductId { get; set; }
    public string SfProductSettingId { get; set; }
    public UnitOfMeasurement UOM { get; set; }
}