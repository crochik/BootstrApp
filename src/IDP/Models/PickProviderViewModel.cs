using System.Collections.Generic;

namespace IDP.Models;

public class PickProviderViewModel
{
    public string ReturnUrl { get; set; }
    public IList<ProviderEntry> Providers { get; set; }

    public class ProviderEntry
    {
        /// <summary>Dictionary key from AppClient.AuthenticationProviders — passed as ?provider= to /account/login.</summary>
        public string Key { get; set; }

        public string DisplayName { get; set; }

        /// <summary>"oidc"/"oauth2" for generic providers; null for built-ins (Google, Microsoft, …).</summary>
        public string Type { get; set; }
    }
}
