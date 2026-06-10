using System;
using System.Collections.Generic;
using System.Linq;
using PI.Shared.Exceptions;
using TimeZoneConverter;

namespace PI.Shared.Models
{
    public interface IEntity : IFlowObject, ICustomProperties
    {
        IEntityContext Context { get; }
        Guid[] GroupMembership { get; }
        IEnumerable<EntityIdentity> GetIdentities();
        string TimeZoneId { get; }
        string Email { get; }
    }

    public static class EntityExtensions
    {
        public static EntityIdentity FirstIdentity(this IEntity entity, ExternalProvider providerId)
            => entity.FirstIdentity(providerId.ToString());

        public static EntityIdentity FirstIdentity(this IEntity entity, string providerId)
            => entity.GetIdentities().FirstOrDefault(x => string.Equals(x.IdentityProviderId, providerId));

        // public static EntityIdentity FindIdentity(this IEntity entity, string providerId, string externalId)
        //     => entity.GetIdentities().FirstOrDefault(x =>
        //         string.Equals(x.IdentityProviderId, providerId) &&
        //         string.Equals(x.ExternalId, externalId));

        public static EntityRoleId GetEntityRoleId(this User user)
        {
            if (Enum.TryParse(typeof(EntityRoleId), user.UserRoleId, true, out object roleIdObj) && roleIdObj is EntityRoleId roleId)
            {
                return roleId;
            }

            throw new Exception("Unexpected Role");
        }

        public static UserContext CreateUserContext(this User user)
        {
            var roleId = user.GetEntityRoleId();
            switch (roleId)
            {
                case EntityRoleId.Root:
                    break;

                case EntityRoleId.Account:
                case EntityRoleId.Admin:
                    if (user.AccountId == Guid.Empty) throw new BadRequestException("Missing Account");
                    break;

                case EntityRoleId.Manager:
                case EntityRoleId.User:
                    if (user.AccountId == Guid.Empty) throw new BadRequestException("Missing Account");
                    if (!user.OrganizationId.HasValue) throw new BadRequestException("Missing Organization");
                    break;

                case EntityRoleId.Profile:
                    if (user.AccountId == Guid.Empty) throw new BadRequestException("Missing Account");
                    return UserContext.Profile(user.Id, user.Name, roleId, user.AccountId, user.OrganizationId);

                default:
                    throw new ForbiddenException($"Unexpected role: {roleId}");
            }

            return roleId == EntityRoleId.Admin ?
                UserContext.Admin(user.Id, user.Name, user.AccountId) :
                UserContext.OrgUser(user.Id, user.Name, roleId, user.OrganizationId.Value, user.AccountId);
        }

        public static OrganizationContext CreateOrgContext(this Organization org) => new(org.Id, org.AccountId);

        [Obsolete("should use object type configuration instead")]
        public static bool CanAccess(this IEntity entity, IEntity other)
            => entity.Context.CanAccess(other.Context);

        [Obsolete("should use object type configuration instead")]
        public static bool CanAccess(this IEntityContext entityContext, IEntity other)
            => entityContext.CanAccess(other.Context);

        // public static bool CanAccess(this IEntity entity, IEntityContext otherContext)
        //     => entity.Context.CanAccess(otherContext);

        /// <summary>
        /// Get timezone info for entity
        /// Will throw if can't find or get a valid one
        /// </summary>
        public static TimeZoneInfo GetTimeZoneInfo(this IEntity entity, string fallbackId = null)
            => TZConvert.GetTimeZoneInfo(entity.TimeZoneId ?? fallbackId);
    }
}