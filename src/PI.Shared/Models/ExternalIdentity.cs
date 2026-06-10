using System.Collections.Generic;
using System.Security.Claims;
using IdentityModel;
using Newtonsoft.Json;

namespace PI.Shared.Models;

/// <summary>
/// ExternalIdentity Model
/// Currently mapped on MapperInitializer
/// </summary>
public class ExternalIdentity
{
    private static readonly Dictionary<string, string> Soap = new()
    {
        { JwtClaimTypes.Name, ClaimTypes.Name },
        { JwtClaimTypes.GivenName, ClaimTypes.GivenName },
        { JwtClaimTypes.FamilyName, ClaimTypes.Surname },
        { JwtClaimTypes.Email, ClaimTypes.Email },
    };

    private static readonly Dictionary<string, string> Okta = new()
    {
        { JwtClaimTypes.Name, JwtClaimTypes.Name },
        { JwtClaimTypes.GivenName, ClaimTypes.GivenName },
        { JwtClaimTypes.FamilyName, ClaimTypes.Surname },
        { JwtClaimTypes.Email, JwtClaimTypes.PreferredUserName },
    };

    private static readonly Dictionary<string, string> InspireNet = new()
    {
        { JwtClaimTypes.Name, JwtClaimTypes.Name },
        { JwtClaimTypes.Email, JwtClaimTypes.PreferredUserName },
    };

    private static readonly Dictionary<string, string> GoToMeeting = new()
    {
        { JwtClaimTypes.GivenName, ClaimTypes.GivenName },
        { JwtClaimTypes.FamilyName, ClaimTypes.Surname },
        { JwtClaimTypes.Email, ClaimTypes.Email },
    };

    private static readonly Dictionary<string, string> RealMagic = new()
    {
        { JwtClaimTypes.Name, JwtClaimTypes.Name },
        { JwtClaimTypes.Email, JwtClaimTypes.Email },
    };

    private static readonly Dictionary<string, string> Salesforce = new()
    {
        { JwtClaimTypes.Email, "urn:salesforce:email" },
    };

    private static readonly Dictionary<string, string> GitHub = new()
    {
    };

    private static readonly Dictionary<string, Dictionary<string, string>> _map = new()
    {
        { nameof(ExternalProvider.Google), Soap },
        { nameof(ExternalProvider.Microsoft), Soap },
        { nameof(ExternalProvider.Okta), Okta },
        { nameof(ExternalProvider.InspireNet), InspireNet },
        { nameof(ExternalProvider.GoToMeeting), GoToMeeting },
        { nameof(ExternalProvider.RealMagic), RealMagic },
        { nameof(ExternalProvider.Salesforce), Salesforce },
        { nameof(ExternalProvider.GitHub), GitHub },
    };

    private static bool IsEmailVerifiedBySalesforce(ExternalIdentity identity)
        => identity.Claims.TryGetValue("email_verified", out var value) ? bool.Parse(value) : false;

    public string Provider { get; set; }

    public string ExternalId { get; set; }
    public Token Token { get; set; }
    public Dictionary<string, string> Claims { get; set; }

    [JsonIgnore]
    public string Name
    {
        get
        {
            return getValue(JwtClaimTypes.Name) ??
                   (
                       GivenName != null ?
                           (FamilyName != null ? $"{GivenName} {FamilyName}" : GivenName) :
                           FamilyName
                   );
        }
        set
        {
            Claims ??= new Dictionary<string, string>();
            Claims[JwtClaimTypes.Name] = value;
        }
    }

    [JsonIgnore]
    public string GivenName => getValue(JwtClaimTypes.GivenName);
    [JsonIgnore]
    public string FamilyName => getValue(JwtClaimTypes.FamilyName);
    [JsonIgnore]
    public string Email => getValue(JwtClaimTypes.Email)?.ToLowerInvariant();

    [JsonIgnore]
    public string TimeZoneId => getValue(JwtClaimTypes.ZoneInfo);

    [JsonIgnore]
    public bool IsVerifiedEmail => Provider switch
    {
        nameof(ExternalProvider.Salesforce) => IsEmailVerifiedBySalesforce(this),
        nameof(ExternalProvider.Google) => true, // ???
        nameof(ExternalProvider.Microsoft) => true, // ???
        nameof(ExternalProvider.InspireNet) => true,
        _ => false
    };

    private string getValue(string claimType)
    {
        if (Claims == null) return null;

        if (_map.TryGetValue(Provider, out var map))
        {
            if (map.TryGetValue(claimType, out var key))
            {
                if (Claims.TryGetValue(key, out var mappedValue))
                {
                    return mappedValue;
                }
            }
        }

        if (Claims.TryGetValue(claimType, out var value))
        {
            return value;
        }

        return null;
    }
}