namespace PI.Salesforce.Models;

public class MaterialAssignment : PI.Shared.Salesforce.Models.SfMaterialAssignment
{
    public string StairsType => InstallationMapLoader.GetSetting(StairsTypeId);
    public string PatternType => InstallationMapLoader.GetSetting(PatternTypeId);
    public string Underlayment => InstallationMapLoader.GetSetting(UnderlaymentId);
    public string InstallType => InstallationMapLoader.GetSetting(InstallTypeId);
    public string ProductType => InstallationMapLoader.GetSetting(ProductTypeId);
    public string SubFloorType => InstallationMapLoader.GetSetting(SubFloorTypeId);
}