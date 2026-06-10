using System.Collections.Generic;

namespace IDP.Models;

public class ConsentViewModel
{
    public string ClientName { get; set; }
    public string ClientUrl { get; set; }
    public string ClientLogoUrl { get; set; }
    public bool AllowRememberConsent { get; set; }
    public bool RememberConsent { get; set; }
    public IEnumerable<string> ScopesConsented { get; set; }
    
    public IEnumerable<ScopeViewModel> IdentityScopes { get; set; }
    public IEnumerable<ScopeViewModel> ApiScopes { get; set; }
    
    public string ReturnUrl { get; set; }
    public string Description { get; set; }
}