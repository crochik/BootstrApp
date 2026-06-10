using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using MongoDB.Driver;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters;

public class EntityIntegrationAdapter : IEntityIntegrationAdapter
{
    private readonly MongoConnection _connection;
    private readonly IIntegrationAdapter _integrationAdapter;

    public EntityIntegrationAdapter(
        MongoConnection connection,
        IIntegrationAdapter integrationAdapter)
    {
        this._connection = connection;
        this._integrationAdapter = integrationAdapter;
    }

    public async Task<EntityIntegration> AddOrUpdateAsync(Guid entityId, EntityIntegration model)
    {
        var dao = MapToDAO(model);

        // TODO: handle update
        // ...

        var result = await _connection.Filter<Entity>()
            .Eq(x => x.Id, entityId)
            .Update.Push(x => x.Integrations, dao)
            .UpdateAndGetOneAsync();

        return result.Integrations.FirstOrDefault(x => x.IntegrationId == model.IntegrationId);
    }

    public async Task<IEnumerable<EntityIntegration>> GetAsync(IEntityContext context)
        => (await _connection.Filter<Entity>()
            .Eq(x => x.Id, context.EntityId)
            .FirstOrDefaultAsync())?.Integrations ?? Array.Empty<EntityIntegration>();

    public async Task<bool> DeleteAsync(IEntityContext context, string serviceName)
    {
        var entityId = context.Role switch
        {
            EntityRoleId.Admin => context.AccountId.Value,
            EntityRoleId.Manager => context.OrganizationId.Value,
            _ => (Guid?)null
        };

        if (!entityId.HasValue)
        {
            // error
            return false;
        }

        var updated = await _connection.Filter<Entity>()
            .Eq(x => x.Id, entityId.Value)
            .Update
            .PullFilter(x => x.Integrations, Builders<EntityIntegration>.Filter.Eq(i => i.ServiceName, serviceName))
            .UpdateAndGetOneAsync();

        return updated != null;
    }

    public async Task<EntityIntegration> FindForEntityAsync(Guid entityId, Guid integrationId)
        => (await _connection.Filter<Entity>()
                .Eq(x => x.Id, entityId)
                .ElemMatchBuilder(
                    x => x.Integrations,
                    f => f.Eq(i => i.IntegrationId, integrationId)
                )
                .FirstOrDefaultAsync())?
            .Integrations.FirstOrDefault(x => x.IntegrationId == integrationId);

    public async Task<IEnumerable<EntityIntegration>> GetForUserAsync(IEntityContext context)
    {
        var result = await _connection.Filter<Entity>()
            .Eq(x => x.Id, context.UserId)
            .FirstOrDefaultAsync();

        return result?.Integrations ?? Array.Empty<EntityIntegration>();
    }

    public Task<IEnumerable<IEntityTrunkIntegration>> GetTrunkByIdAsync(Guid entityId, Guid integrationId)
        => GetTrunkByIdAsync(_connection, entityId, integrationId);
        
    public static async Task<IEnumerable<IEntityTrunkIntegration>> GetTrunkByIdAsync(MongoConnection connection, Guid entityId, Guid integrationId)
    {
        var entity = await connection.Filter<Entity>()
            .Eq(x => x.Id, entityId)
            .FirstOrDefaultAsync();

        if (entity == null) return Array.Empty<IEntityTrunkIntegration>();

        var entities = entity.Context.GetEntityIds().ToArray();
        if (entities.Length == 1)
        {
            return entity.Integrations
                .Where(x => x.IntegrationId == integrationId)
                .Select(x => Map(entity, x));
        }

        var result = await connection.Filter<Entity>()
            .In(x => x.Id, entities)
            .ElemMatchBuilder(x => x.Integrations, 
                f => f.Eq(i => i.IntegrationId, integrationId)
                // Builders<EntityIntegration>.Filter.Eq(i => i.IntegrationId, integrationId)
            )
            .FindAsync();

        return Map(result, integrationId);
    }

    private static IEnumerable<EntityTrunkIntegration> Map(IEnumerable<Entity> entities, Guid integrationId)
    {
        foreach (var entity in entities)
        {
            foreach (var i in entity.Integrations.Where(x => x.IntegrationId == integrationId))
            {
                yield return Map(entity, i);
            }
        }
    }

    private IEnumerable<EntityTrunkIntegration> Map(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            foreach (var i in entity.Integrations)
            {
                yield return Map(entity, i);
            }
        }
    }

    private static EntityTrunkIntegration Map(IEntity entity, EntityIntegration integration)
    {
        var level = entity.Context.Role switch
        {
            EntityRoleId.User => EntityTrunkLevel.User,
            EntityRoleId.Manager => EntityTrunkLevel.User,
            EntityRoleId.Admin => EntityTrunkLevel.User,
            EntityRoleId.Organization => EntityTrunkLevel.Organization,
            EntityRoleId.Account => EntityTrunkLevel.Account,
            _ => throw new ForbiddenException(entity.Context)
        };

        return new EntityTrunkIntegration
        {
            IntegrationId = integration.IntegrationId,
            Data = integration.Data,
            Authentication = integration.Authentication,
            Level = level
        };
    }

    private EntityIntegration MapToDAO(EntityIntegration model)
    {
        // TODO: have to handle different integrations
        // ... 

        var dao = _connection.Map<EntityIntegration>(model);
        dao.ServiceName = _integrationAdapter.GetById(model.IntegrationId)?.ServiceName;
        return dao;
    }

    public class EntityIntegrationProfile : Profile
    {
        public EntityIntegrationProfile()
        {
            // TODO: handle different integrations
            // ...
            CreateMap<EntityIntegration, EntityIntegration>()
                .ForMember(x => x.ServiceName, o => o.Ignore());
        }
    }
}