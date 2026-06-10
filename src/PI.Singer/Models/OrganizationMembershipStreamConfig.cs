using PI.Shared.Models;

namespace Models
{
    public class OrganizationMembershipStreamConfig : SingerStreamConfig
    {
        public ExternalProvider ExternalProvider { get; set; }
        public string UserExternalIdField { get; set; } = "serviceResourceId";
        public string OrganiztionExternalIdField { get; set; } = "serviceTerritoryId";
        public bool AllowReassignment { get; set; } = true;
    }
}