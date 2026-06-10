using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.CompanyCam.Models;

public class Token
{
    [JsonProperty("access_token")] 
    public string AccessToken { get; set; }

    [JsonProperty("token_type")] 
    public string TokenType { get; set; }

    [JsonProperty("refresh_token")] 
    public string RefreshToken { get; set; }
    
    [JsonProperty("expires_in")] 
    public int ExpiresIn { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [BsonElement]
    public DateTime ExpiresOn { get; set; }
}