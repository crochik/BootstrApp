using System;
using Microsoft.AspNetCore.Authentication.OAuth;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

[BsonDiscriminator]
public class Token
{
    public string AccessToken { get; set; }
    public string TokenType { get; set; }
    public string RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public DateTime Expiration { get; set; }
    public bool HasExpired => DateTime.UtcNow.CompareTo(Expiration) >= 0;

    public Token()
    {

    }

    public Token(OAuthTokenResponse response)
    {
        AccessToken = response.AccessToken;
        TokenType = response.TokenType;
        RefreshToken = response.RefreshToken;
        ExpiresIn = int.Parse(response.ExpiresIn);
        Expiration = DateTime.UtcNow.AddSeconds(ExpiresIn);
    }
}