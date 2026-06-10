using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models.Client;

namespace PI.Shared.Models;

[Obsolete]
public class AppClientProfiles
{
    public Guid? Admin { get; set; }
    public Guid? Manager { get; set; }
    public Guid? User { get; set; }
}

public class AppClientProfile
{
    /// <summary>
    /// Profile Id
    /// When set, is the same for any account
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Profile Name
    /// When set, will lookup for the account and fall back to default
    /// </summary>
    public string Name { get; set; }
}

public class AutoProvisionUser
{
    /// <summary>
    /// If defined, allows users to be auto provisioned on demand
    /// </summary>
    public EntityRoleId? UserRole { get; set; }

    public Guid? UserFlowId { get; set; }

    public Guid? OrganizationFlowId { get; set; }
}

public class TenantConfiguration
{
    /// <summary>
    /// Whenever specified for a client without AccountId, will indicate the default account (for new users)
    /// </summary>
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Allow auto-provisioning for new users
    /// </summary>
    public AutoProvisionUser AutoProvisionUser { get; set; }

    /// <summary>
    /// Default profiles for each role
    /// </summary>
    public Dictionary<string, AppClientProfile> AppProfiles { get; set; }
}

public class AuthenticationProvider
{
    public Dictionary<string, TenantConfiguration> Tenants { get; set; }

    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string Authority { get; set; }
    public string CallbackPath { get; set; }
    public string[] Scopes { get; set; }
    public bool? SaveTokens { get; set; }
    public string Type { get; set; }
    public string DisplayName { get; set; }
    public string AuthorizationEndpoint { get; set; }
    public string TokenEndpoint { get; set; }
    public string UserInformationEndpoint { get; set; }
    public Dictionary<string, string> ClaimMappings { get; set; }
    public string SubjectClaim { get; set; }
    public string EmailClaim { get; set; }
    public string NameClaim { get; set; }
    
    public string EmailTemplate { get; set; }
    public string SmsSenderId { get; set; } // declared but not yet consumed in IDP    
}

[BsonCollection("idp.Client")]
public class AppClient
{
    [BsonId] public string ClientId { get; set; }

    [Obsolete("use AppProfiles instead")] public AppClientProfiles AppClientProfiles { get; set; }

    /// <summary>
    /// Default profiles for this client (by role)
    /// replaces AppClientProfiles
    /// </summary>
    public Dictionary<string, AppClientProfile> AppProfiles { get; set; }

    /// <summary>
    /// since the id can contain '.', use this (optional) value to look for a profile in the user for this client
    /// </summary>
    public string ProfileKey { get; set; }

    /// <summary>
    /// When specified makes the client exclusive to an account
    /// </summary>
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Authentication providers for this client
    /// key is the provider (e.g. Microsoft, Salesforce, Google, ...)
    /// </summary>
    public Dictionary<string, AuthenticationProvider> AuthenticationProviders { get; set; }

    /// <summary>
    /// If this client supports anonymous authentication, what user id to use
    /// Requires that the user exists in the account for this client
    /// </summary>
    public Guid? AnonymousUserId { get; set; }

    /// <summary>
    /// Claims calculated expressions using User + Organization + Profile
    /// </summary>
    public Dictionary<string, string> CalculatedClaims { get; set; }

    public bool Enabled { get; set; } = true;
    public string ProtocolType { get; set; } = "oidc";
    public List<Secret> ClientSecrets { get; set; }
    public bool RequireClientSecret { get; set; } = true;
    public string ClientName { get; set; }
    public string Description { get; set; }
    public string ClientUri { get; set; }
    public string LogoUri { get; set; }
    public bool RequireConsent { get; set; } = false;
    public bool AllowRememberConsent { get; set; } = true;
    public bool AlwaysIncludeUserClaimsInIdToken { get; set; }
    public List<ClientGrantType> AllowedGrantTypes { get; set; }
    public bool RequirePkce { get; set; }
    public bool AllowPlainTextPkce { get; set; }
    public bool AllowAccessTokensViaBrowser { get; set; }
    public List<ClientRedirectUri> RedirectUris { get; set; }
    public List<ClientPostLogoutRedirectUri> PostLogoutRedirectUris { get; set; }
    public string FrontChannelLogoutUri { get; set; }
    public bool FrontChannelLogoutSessionRequired { get; set; } = true;
    public string BackChannelLogoutUri { get; set; }
    public bool BackChannelLogoutSessionRequired { get; set; } = true;
    public bool AllowOfflineAccess { get; set; }
    public List<ClientScope> AllowedScopes { get; set; }
    public int IdentityTokenLifetime { get; set; } = 300;
    public int AccessTokenLifetime { get; set; } = 3600;
    public int AuthorizationCodeLifetime { get; set; } = 300;
    public int? ConsentLifetime { get; set; } = null;
    public int AbsoluteRefreshTokenLifetime { get; set; } = 2592000;
    public int SlidingRefreshTokenLifetime { get; set; } = 1296000;
    public int RefreshTokenUsage { get; set; } = 1; // TokenUsage.OneTimeOnly;
    public bool UpdateAccessTokenClaimsOnRefresh { get; set; }
    public int RefreshTokenExpiration { get; set; } = 1; // TokenExpiration.Absolute;
    public int AccessTokenType { get; set; } = (int)0; // AccessTokenType.Jwt;
    public bool EnableLocalLogin { get; set; }
    public List<ClientIdPRestriction> IdentityProviderRestrictions { get; set; }
    public bool IncludeJwtId { get; set; }
    public List<ClientClaim> Claims { get; set; }
    public bool AlwaysSendClientClaims { get; set; }
    public string ClientClaimsPrefix { get; set; } = "client_";
    public string PairWiseSubjectSalt { get; set; }

    /// <summary>
    /// it has to include absolute origins for IdentityService to allow user to login
    /// it can include hosts, and even wildcards (e.g. *.fci.cloud) to allow API
    ///     when the api doesn't recognize the origin a warning message is logged
    /// </summary>
    public List<ClientCorsOrigin> AllowedCorsOrigins { get; set; }

    public List<Property> Properties { get; set; }

    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? Updated { get; set; }
    public DateTime? LastAccessed { get; set; }
    public int? UserSsoLifetime { get; set; }
    public string UserCodeType { get; set; }
    public int DeviceCodeLifetime { get; set; } = 300;
    public bool NonEditable { get; set; }

    /// <summary>
    /// Specifies whether the client must use a request object on authorize requests (defaults to <c>false</c>.)
    /// </summary>
    public bool RequireRequestObject { get; set; } = false;

    /// <summary>
    /// Signing algorithm for identity token. If empty, will use the server default signing algorithm.
    /// </summary>
    public string[] AllowedIdentityTokenSigningAlgorithms { get; set; } = [];
}