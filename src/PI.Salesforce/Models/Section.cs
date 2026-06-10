using PI.Salesforce.Models;

namespace PI.Shared.Salesforce.Models;

public class Section : SfSection
{
    public string InstallType => InstallationMapLoader.GetSetting(InstallTypeId);

    public string ProductType => InstallationMapLoader.GetSetting(ProductTypeId);
    public string TrimWork => InstallationMapLoader.GetSetting(TrimWorkId);
    public string PatternType => InstallationMapLoader.GetSetting(PatternTypeId);
    public string StairsType => InstallationMapLoader.GetSetting(StairsTypeId);
    public string SubfloorType => InstallationMapLoader.GetSetting(SubfloorTypeId);
    public string Underlayment => InstallationMapLoader.GetSetting(UnderlaymentId);
    
    public SfSectionLineItem[] SectionLineItems { get; set; }
    public SfExternalLink[] ExternalLinks { get; set; }
    public Room[] Rooms { get; set; }
}