namespace PI.DocuSeal.Models;

public class DocuSealConfiguration
{
    public const string SectionName = "DocuSeal";

    public string ApiUrl { get; set; } = "https://api.docuseal.com/";
    public string ApiKey { get; set; } 
}