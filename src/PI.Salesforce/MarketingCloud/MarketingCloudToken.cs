using System;
using Newtonsoft.Json;

namespace Services;

public class MarketingCloudToken
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }
    
    [JsonProperty("token_type")]
    public string TokenType { get; set; }
    
    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonProperty("scope")]
    public string Scope { get; set; }

    [JsonProperty("soap_instance_url")]
    public string SoapUrl { get; set; }
        
    [JsonProperty("rest_instance_url")]
    public string RestUrl { get; set; }

    public string[] Scopes => Scope?.Split(' ');
    public DateTime CreatedOn { get; } = DateTime.UtcNow;
    public DateTime Expiration => CreatedOn.AddSeconds(ExpiresIn);
}