using PI.Shared.Models;

namespace PI.Shared.Salesforce.Models;

public class SalesforceToken : Token
{
    public string InstanceUrl { get; set; }
    public string ApiVersion { get; set; }
}