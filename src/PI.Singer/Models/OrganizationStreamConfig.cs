using PI.Shared.Models;

namespace Models
{
    public class OrganizationStreamConfig : SingerStreamConfig
    {
        public ExternalProvider ExternalProvider { get; set; }
        public string ExternalIdField { get; set; } = "id";
        public string NameField { get; set; } = "name";

        public OrganizationStreamConfig() { }
    }
}