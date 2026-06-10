using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{

    public class UserAdapter : EntityAdapter<User, User>, IUserAdapter
    {
        private readonly ILogger<UserAdapter> _logger;

        public UserAdapter(
            ILogger<UserAdapter> logger,
            MongoConnection connection
            ) : base(connection)
        {
            this._logger = logger;
        }

        public async Task<User> CreateAsync(IEntityContext context, User user, EntityIdentity identity = null)
        {
            var dao = _connection.Map<User>(user);
            dao.CreatedOn = DateTime.UtcNow;
            dao.LastModifiedOn = DateTime.UtcNow;
            dao.LastActor = context.Actor();

            if (identity != null)
            {
                var iDao = _connection.Map<EntityIdentity>(identity);
                dao.Identities = new[] { iDao };
                dao.MainIdentityId = iDao.Id;
            }

            await _connection.InsertAsync<Entity>(dao);

            return await GetByIdAsync(dao.Id);
        }

        public async Task<User> SetOrganization(IEntityContext context, User user, Guid organizationId)
        {
            var result = await _connection.FindOneAndUpdateAsync<Entity, User, Guid>(
                user.Id,
                Builders<User>.Update
                    .Set(x => x.OrganizationId, organizationId)
                    .Set(x => x.EntityId, organizationId)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
            );

            return result;
        }

        public async Task<User> UpdateAsync(User user)
        {
            var result = await _connection.FindOneAndUpdateAsync<Entity, User, Guid>(
                user.Id,
                Builders<User>.Update
                    .Set(x => x.AccountId, user.AccountId)
                    .Set(x => x.OrganizationId, user.OrganizationId)
                    .Set(x => x.EntityId, user.OrganizationId)
                    .Set(x => x.Name, user.Name)
                    .Set(x => x.MainIdentityId, user.MainIdentityId)
                    .Set(x => x.UserRoleId, user.UserRoleId)
                    .Set(x => x.IsActive, user.IsActive)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            );

            return result;
        }

        public async Task<bool> UpdateIsActiveAsync(IEntityContext context, Guid id, bool isActive)
        {
            var result = await _connection.Filter<Entity>()
                .Eq(x => x.Id, id)
                .OfType<Entity, User>()
                .Update
                    .Set(x => x.IsActive, isActive)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
                .UpdateOneAsync();

            return result.MatchedCount == 1;
        }

        public Task<bool> DeleteAsync(Guid entityId)
        {
            throw new NotImplementedException();
        }

        public async Task<User> FindForIdentityAsync(string loginProvider, string providerKey)
        {
            var user = await _connection.Filter<User>()
                .Eq("_t", nameof(User))
                .ElemMatchBuilder(x => x.Identities,
                    f => f.Eq(i => i.IdentityProviderId, loginProvider)
                        .Eq(i => i.ExternalId, providerKey)
                ).FirstOrDefaultAsync();

            return user;
        }

        public async Task<(User Entity, EntityIdentity Identity)> FindForIdentityAsync(IEntityContext context, string loginProvider, string externalId)
        {
            var entity = await _connection.Filter<User>()
                .Eq("_t", nameof(User))
                .Eq(x => x.AccountId, context.AccountId.Value)
                .ElemMatchBuilder(x => x.Identities,
                    f => f.Eq(i => i.IdentityProviderId, loginProvider)
                        .Eq(i => i.ExternalId, externalId)
                ).FirstOrDefaultAsync();

            var identity = entity?.FindIdentity(loginProvider, externalId);
            return identity != null ? (entity, identity) : (null, null);
        }

        public async Task<IEnumerable<User>> GetAsync(IEntityContext context)
        {
            var query = _connection.Filter<User>()
                .Eq("_t", nameof(User))
                .Eq(x => x.AccountId, context.AccountId.Value);

            switch (context.Role)
            {
                case EntityRoleId.Account:
                case EntityRoleId.Admin:
                    break;

                case EntityRoleId.Organization:
                case EntityRoleId.Manager:
                    query.Eq(x => x.OrganizationId, context.OrganizationId.Value);
                    break;

                case EntityRoleId.User:
                    query.Eq(x => x.Id, context.UserId.Value);
                    break;

                default:
                    return Array.Empty<User>();
            }

            query
                .IncludeField(x => x.Id)
                .IncludeField(x => x.Name)
                .IncludeField(x => x.IsActive)
                .IncludeField(x => x.UserRoleId)
                .IncludeField(x => x.OrganizationId)
                .IncludeField(x => x.AccountId);

            return await query.FindAsync();
        }

        public async Task<IEnumerable<User>> GetAvaialbleForAppointmentAsync(Guid apptTypeId)
        {
            // ????
            // { "Availability" : { "$elemMatch" : { "AppointmentTypeIds" : "2d665249-c6c4-4b95-8855-e334caa482ee" } }, "_t" : "User" }
            // {"Availability.AppointmentTypeIds" : "2d665249-c6c4-4b95-8855-e334caa482ee", "_t" : "User"}
            var users = await _connection.Filter<User>()
                .Eq("_t", nameof(User))
                .ElemMatchBuilder(x => x.Availability, f => f.AnyEq(a => a.AppointmentTypeIds, apptTypeId))
                .FindAsync();

            return users;
        }

        public Task<IEnumerable<MergeUserCandidateMatch>> GetMergeCandidatesAsync(Guid accountId)
            => Task.FromResult<IEnumerable<MergeUserCandidateMatch>>(Array.Empty<MergeUserCandidateMatch>());

        public Task<User> MergeAsync(User target, User other)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> UpdateExternalIdentiyAsync(User user, ExternalIdentity externalIdentity)
        {
            var providerId = externalIdentity.Provider.ToString();

            var result = await _connection.Filter<User>()
                .Eq(x => x.Id, user.Id)
                .ElemMatchBuilder(
                    x => x.Identities,
                    f => f.Eq(i => i.IdentityProviderId, providerId)
                        .Eq(x => x.ExternalId, externalIdentity.ExternalId)
                )
                .Update
                    .Set($"{nameof(Entity.Identities)}.$.{nameof(EntityIdentity.ExternalIdentity)}", externalIdentity)
                .UpdateAndGetOneAsync();

            if (result == null)
            {
                _logger.LogError("Couldn't update {userId}: {providerId}/{externalId}", user.Id, providerId, externalIdentity.ExternalId);
                return false;
            }

            if (string.IsNullOrEmpty(result.Email) && !string.IsNullOrEmpty(externalIdentity.Email) && externalIdentity.IsVerifiedEmail)
            {
                await _connection.Filter<User>()
                    .Eq(x=>x.Id, result.Id)
                    .Update.Set(x=>x.Email, externalIdentity.Email)
                    .UpdateOneAsync();
            }

            return true;
        }
    }
}