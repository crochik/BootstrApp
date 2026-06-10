namespace PI.CompanyCam.Models;

public class CCConfiguration
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string[] Scopes { get; set; } = new[] { "read", "write", "destroy" }; 
}