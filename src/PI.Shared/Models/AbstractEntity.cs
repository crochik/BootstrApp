using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Constants;

namespace PI.Shared.Models
{
    [DiscriminatorWithFallback]
    [BsonDiscriminator(Required = true)]
    [BsonKnownTypes(typeof(UserSettings), typeof(OrganizationSettings), typeof(AccountSettings))]
    public class EntitySettings
    {
        /// Generic settings
        public Dictionary<string, Dictionary<string, object>> Section { get; set; }
    }

    public class UserSettings : EntitySettings
    {
    }

    public class OrganizationSettings : EntitySettings
    {
    }

    public class AccountSettings : EntitySettings
    {
        public Guid? OwnerId { get; set; }
     
        // ??????
        // public string[] ClientIds { get; set; }
    }
    
    public class EntityIdentity : IRow<Guid>, ISupportInitialize
    {
        [BsonId]
        public Guid Id { get; set; }

        public string IdentityProviderId { get; set; }
        public string ExternalId { get; set; }
        public ExternalIdentity ExternalIdentity { get; set; }
        public string Name { get; set; }
        public Dictionary<string, object> Data { get; set; }

        public void BeginInit()
        {
        }

        public void EndInit()
        {
            // we don't serialize the ExternalIdentity.Provider and ExternalIdentity.ExternalId
            // copy from identity
            if (ExternalIdentity != null)
            {
                ExternalIdentity.Provider = IdentityProviderId;
                ExternalIdentity.ExternalId = ExternalId;
            }
        }

        public bool TryGetSalesforceFieldValue<T>(string salesforceFieldName, out T value)
            where T : class
        {
            if (Data == null)
            {
                value = default(T);
                return false;
            }

            if (Data.TryGetValue(salesforceFieldName, out var objValue) && objValue is T)
            {
                value = (T)objValue;
                return true;
            }

            var singer = ComputeSingerFieldName(salesforceFieldName);
            if (Data.TryGetValue(singer, out objValue) && objValue is T)
            {
                value = (T)objValue;
                return true;
            }

            value = default(T);
            return false;
        }

        public T GetSalesforceFieldValue<T>(string salesforceFieldName, T defaultValue)
            where T : class
        {
            if (TryGetSalesforceFieldValue<T>(salesforceFieldName, out T value))
            {
                return value;
            }

            return defaultValue;
        }

        public static string ComputeSingerFieldName(string salesforceFieldName)
        {
            // Branch_Territory_Name__c => branchTerritoryNameC
            // INET_WhoIsFCI__c => inetWhoIsFciC
            // Branch_Phone_Number__c => branchPhoneNumberC
            var parts = salesforceFieldName.Split('_', StringSplitOptions.RemoveEmptyEntries);
            var builder = new StringBuilder(Char.ToLowerInvariant(parts[0][0]) + parts[0][1..]);
            for (var c=1; c<parts.Length; c++)
            {
                builder.Append(Char.ToUpperInvariant(parts[c][0]) + parts[c][1..]);
            }

            var result = builder.ToString();
            return result;
        }
    }

    /// <summary>
    /// mapped on the mapperinitializer so it can use encryptedserializer
    /// </summary>
    [DiscriminatorWithFallback]
    [BsonDiscriminator(Required = true)]
    [BsonKnownTypes(
        typeof(O365Integration),
        typeof(SchedulerEntityIntegration)
    )]
    public class EntityIntegration : IIntegration
    {
        public Guid IntegrationId { get; set; }

        /// <summary>
        /// Json configuration for integration
        /// </summary>
        [Obsolete("extend the EntityIntegration class instead")]
        public string Data { get; set; }

        /// <summary>
        /// Authentication info for the integration
        /// It is encrypted using EncryptedStringSerializer 
        /// </summary>
        public string Authentication { get; set; }

        public string ServiceName { get; set; }
        public bool IsActive { get; set; } = true;
    }

    [DiscriminatorWithFallback]
    [BsonDiscriminator(Required = true)]
    [BsonKnownTypes(typeof(User), typeof(Organization), typeof(Account))]
    public class Entity : FlowObjectModel, IEntity
    {
        public EntityIdentity[] Identities { get; set; }
        public EntityIntegration[] Integrations { get; set; }
        public Guid[] GroupMembership { get; set; }
        public string TimeZoneId { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        [BsonIgnore]
        public IEntityContext Context => _context ??= GetContext();

        public Dictionary<string, object> Properties { get; set; }

        [BsonIgnore]
        private IEntityContext _context = null;

        protected virtual IEntityContext GetContext() => null;
        public EntityIdentity FindIdentity(string provider, string externalId)
            => Identities?.FirstOrDefault(x => string.Equals(provider, x.IdentityProviderId) && string.Equals(externalId, x.ExternalId));
        public IEnumerable<EntityIdentity> GetIdentities() => Identities ?? Enumerable.Empty<EntityIdentity>();

        protected Entity()
        {

        }
    }

    /// <summary>
    /// Used to associate entities (no api support yet)
    /// </summary>
    [BsonCollection(nameof(EntityGroup))]
    public class EntityGroup : EntityOwnedModel
    {
    }

    [BsonCollection(nameof(Entity))]
    public class Account : Entity
    {
        public override string ObjectType => SystemObjectType.Account;
        public AccountSettings Settings { get; set; }
        protected override IEntityContext GetContext() => new AccountContext(Id);
    }

    [BsonCollection("Entity.Counters")]
    public class EntityCounter : IdOnlyModel
    {
        public Guid EntityId { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
    }
    
    [BsonDiscriminator("o365")]
    public class O365Integration : EntityIntegration
    {
        public DateTime? LastSyncedOn { get; set; }
        public bool UsesAccountAuth { get; set; }
        public O365Subscription Subscription { get; set; }

        public O365Integration()
        {
            ServiceName = "O365";
            IntegrationId = IntegrationIds.Office365;
        }
    }

    [BsonDiscriminator("scheduler")]
    public class SchedulerEntityIntegration : EntityIntegration
    {
        /// <summary>
        /// Base url for public scheduler (used to build redirections)
        /// </summary>
        public string BaseUrl { get; set; }
        
        /// <summary>
        /// Client Id of scheduler app (used to create JWT used in redirections)
        /// </summary>
        public string ClientId { get; set; }
        
        public string[] AlternativeClientIds { get; set; }

        public SchedulerEntityIntegration()
        {
            ServiceName = nameof(IntegrationIds.AutoScheduler);
            IntegrationId = IntegrationIds.AutoScheduler;
        }
    }

    [BsonCollection(nameof(Entity))]
    // [Policy(EntityRoleId.Account)]
    // [Policy(EntityRoleId.Organization)]
    public class User : Entity
    {
        public override string ObjectType => SystemObjectType.User;

        public Guid? OrganizationId { get; set; }

        public string UserRoleId { get; set; }

        public Guid? MainIdentityId { get; set; }

        public Availability[] Availability { get; set; }

        public Dictionary<string, Guid> AppProfiles { get; set; }

        public UserSettings Settings { get; set; }

        protected override IEntityContext GetContext() => this.CreateUserContext();
    }

    [BsonCollection(nameof(Entity))]
    // [Policy(EntityRoleId.Account)]
    public class Organization : Entity
    {
        public override string ObjectType => SystemObjectType.Organization;
        public OrganizationSettings Settings { get; set; }
        protected override IEntityContext GetContext() => this.CreateOrgContext();
    }
}
