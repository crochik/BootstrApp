using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.Client
{
    public class BaseResource
    {
        public static readonly IEqualityComparer<BaseResource> Comparer = new EqualityComparer();

        [BsonId]
        public string Name { get; set; }
        public bool Enabled { get; set; } = true;
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public List<UserClaim> UserClaims { get; set; } = new();
        public List<Property> Properties { get; set; }
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Updated { get; set; }
        public bool NonEditable { get; set; }

        private class EqualityComparer : IEqualityComparer<BaseResource>
        {
            public bool Equals([AllowNull] BaseResource x, [AllowNull] BaseResource y)
            {
                return string.Equals(x?.Name, y?.Name);
            }

            public int GetHashCode([DisallowNull] BaseResource obj)
            {
                return obj.Name?.GetHashCode() ?? 0;
            }
        }
    }

    [BsonCollection("idp.IdentityResource")]
    public class IdentityResource : BaseResource
    {
        public bool Required { get; set; }
        public bool Emphasize { get; set; }
        public bool ShowInDiscoveryDocument { get; set; } = true;
    }

    [BsonCollection("idp.ApiResource")]
    public class ApiResource : BaseResource
    {
        public List<Secret> Secrets { get; set; }
        public List<ApiScope> Scopes { get; set; }
        public DateTime? LastAccessed { get; set; }

        /// <summary>
        /// Signing algorithm for access token. If empty, will use the server default signing algorithm.
        /// </summary>
        public string[] AllowedAccessTokenSigningAlgorithms { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Specifies whether this scope is shown in the discovery document. Defaults to true.
        /// </summary>
        public bool ShowInDiscoveryDocument { get; set; } = true;
    }
}
