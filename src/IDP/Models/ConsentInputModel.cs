using System.Collections.Generic;

namespace IDP.Models;

public class ConsentInputModel
{
    public string Button { get; set; }  // "yes" or "no"
    public IEnumerable<string> ScopesConsented { get; set; }
    public bool RememberConsent { get; set; }
    public string ReturnUrl { get; set; }
    public string Description { get; set; }
}