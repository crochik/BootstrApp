using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class LeadStatusAdapter : ILeadStatusAdapter
    {
        private readonly MongoConnection _connection;

        public LeadStatusAdapter(MongoConnection connection)
        {
            this._connection = connection;
        }

        public async Task<ILeadStatus> CreateAsync(ILeadStatus leadStatus)
        {
            var model = _connection.Map<ObjectStatus>(leadStatus);
            // model.LastActor = 
            await _connection.InsertAsync(model);
            return model;
        }

        public Task<bool> DeleteAsync(Guid id)
            => _connection.Filter<ObjectStatus>()
                .Eq(x => x.ObjectType, SystemObjectType.Lead)
                .Eq(x => x.Id, id)
                .DeleteOneAsync();

        public async Task<ILeadStatus> GetByIdAsync(Guid id)
            => await _connection.Filter<ObjectStatus>()
                .Eq(x => x.ObjectType, SystemObjectType.Lead)
                .Eq(x => x.Id, id)
                .FirstOrDefaultAsync();

        public async Task<IEnumerable<ILeadStatus>> GetTrunkAsync(IEntityContext context)
        {
            return await _connection.Filter<ObjectStatus>()
                .Eq(x => x.ObjectType, SystemObjectType.Lead)
                .In(x => x.EntityId, GetEntityIds(context))
                .FindAsync();
        }

        public async Task<bool> UpdateAsync(ILeadStatus leadStatus)
        {
            var model = _connection.Map<ObjectStatus>(leadStatus);
            // model.LastActor = 

            var result = await _connection.Filter<ObjectStatus>()
                .Eq(x => x.ObjectType, SystemObjectType.Lead)
                .Eq(x => x.Id, model.Id)
                .ReplaceOneAsync(model);

            return result.MatchedCount == 1;
        }

        private IEnumerable<Guid?> GetEntityIds(IEntityContext context)
        {
            yield return null;
            yield return context.AccountId;

            switch (context.Role)
            {
                case EntityRoleId.Account:
                    break;

                case EntityRoleId.Admin:
                    yield return context.UserId;
                    break;

                case EntityRoleId.Organization:
                    yield return context.OrganizationId;
                    break;

                case EntityRoleId.Manager:
                case EntityRoleId.User:
                    yield return context.OrganizationId;
                    yield return context.UserId;
                    break;

                default:
                    throw new ForbiddenException(context);
            }
        }
    }

    public class LeadStatusProfile : Profile
    {
        public LeadStatusProfile()
        {
            CreateMap<ILeadStatus, ObjectStatus>()
                .ForMember(d => d.ObjectType, o => o.MapFrom(s => SystemObjectType.Lead))
                .ForMember(d=>d.ObjectTypeId, o => o.MapFrom(s => ObjectTypeIds.Lead))
                .ForMember(d => d.CreatedOn, o => o.MapFrom(s => DateTime.UtcNow))
                .ForMember(d => d.LastActor, o => o.Ignore())
                .ForMember(d => d.LastModifiedOn, o => o.Ignore())
                ;
        }
    }
}