using System.Collections.Generic;
using PI.Shared.Models;

namespace Models
{
    public class UserStreamConfig : SingerStreamConfig
    {
        public ExternalProvider ExternalProvider { get; set; }
        public string ExternalIdField { get; set; } = "id";
        public string UserNameField { get; set; } = "name";
        public Dictionary<string, string> AdditionalExternalIdFields { get; set; }
        public string UpdateIdentityField { get; set; }

        public UserStreamConfig() { }
    }
}