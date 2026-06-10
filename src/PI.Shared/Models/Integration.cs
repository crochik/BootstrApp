using System;
using System.Collections.Generic;
using System.Linq;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

public enum ApiAuthType
{
    /// <summary>No authentication required.</summary>
    None,

    /// <summary>Uses a single secret string, typically sent in a header (e.g., 'X-API-Key') or as a query parameter.</summary>
    ApiKey,

    /// <summary>Uses a Bearer Token (e.g., OAuth 2.0 access token) sent in the 'Authorization' header.</summary>
    BearerToken,

    /// <summary>Uses username and password encoded in the 'Authorization' header.</summary>
    BasicAuth,
}

/// <summary>
/// Integration ...
/// it is not a "model" since it is not tied to an account
/// </summary>
public class Integration : ITaggable
{
    [BsonId]
    [BsonSerializer(typeof(MagicGuidSerializer))]
    public Guid Id { get; set; }

    public string Name { get; set; }
     
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedOn { get; set; }
    public Actor LastActor { get; set; }

    /// <summary>
    /// Auth config to use when accessing API
    /// </summary>
    public ApiAuthType AuthType { get; set; }
    
    // api key
    // header name
    // parameter name 
    
    public string[] Tags { get; set; }
}

/// <summary>
/// Configuration for an integration
/// </summary>
[DiscriminatorWithFallback]
[BsonCollection("IntegrationConfiguration")]
[BsonDiscriminator(Required = true)]
public class IntegrationConfiguration : EntityOwnedModel
{
    public Guid IntegrationId { get; set; }
}

public static class IntegrationConfigurationExtensions
{
    /// <summary>
    /// Get More specific configuration (user->organization->account)
    /// </summary>
    public static T GetMoreSpecific<T>(this IEnumerable<T> list, IEntityContext context) where T : IntegrationConfiguration
    {
        return list?.MinBy(getPriority);

        int getPriority(T obj)
        {
            if (context.UserId.HasValue && context.UserId.Value == obj.EntityId) return 1;
            if (context.OrganizationId.HasValue && context.OrganizationId.Value == obj.EntityId) return 2;
            if (context.AccountId == obj.EntityId) return 3;
            return 10;
        }
    }
}

public class IntegrationToken
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresOn { get; set; }
}

/// <summary>
/// Integration Configuration with token
/// </summary>
public class IntegrationConfigurationWithToken : IntegrationConfiguration
{
    public IntegrationToken Token { get; set; }
    public string PersonalAccessToken { get; set; }
}