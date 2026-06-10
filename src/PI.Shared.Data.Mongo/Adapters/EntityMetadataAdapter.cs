using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class EntityMetadataAdapter : IEntityMetadataAdapter
    {
        private readonly MongoConnection _connection;

        public EntityMetadataAdapter(MongoConnection connection)
        {
            this._connection = connection;
        }

        public async Task<IEnumerable<IEntityMetadata>> AddForEntityAsync(Guid organizationId, IEntityMetadata[] list, bool exclusive = true)
        {
            var daos = list.Select(x => new EntityMetadata
            {
                EntityId = organizationId,
                PartitionId = x.PartitionId,
                Key = x.Key,
                Value = x.Value
            }).ToArray();

            if (exclusive)
            {
                await DeleteAsync(daos);
            }

            await _connection.InsertManyAsync(daos);

            return daos;
        }

        public async Task<IEnumerable<IEntityMetadata>> DeleteAsync(IEntityMetadata[] list)
        {
            var filters = list.Select(i =>
                Builders<EntityMetadata>.Filter
                    .Eq(x => x.PartitionId, i.PartitionId)
                    .Eq(x => x.Key, i.Key)
                    .Eq(x => x.Value, i.Value)
            );

            var result = await _connection.Filter<EntityMetadata>()
                .Or(filters)
                .DeleteAsync();

            return list;
        }

        public async Task<IEnumerable<IEntityMetadata>> FindAsync(Guid partitionId, string key, string value)
        {
            return await _connection.Filter<EntityMetadata>()
                .Eq(x => x.PartitionId, partitionId)
                .Eq(x => x.Key, key)
                .Eq(x => x.Value, value)
                .FindAsync();
        }

        public async Task<IEnumerable<IEntityMetadata>> GetAsync(Guid partitionId, Guid entityId, string key)
        {
            return await _connection.Filter<EntityMetadata>()
                .Eq(x => x.PartitionId, partitionId)
                .Eq(x => x.EntityId, entityId)
                .Eq(x => x.Key, key)
                .FindAsync();
        }

        public async Task<IEnumerable<IEntityMetadata>> GetAsync(IEntityContext context)
        {
            var entityId = context.Role switch
            {
                EntityRoleId.Account => context.AccountId,
                EntityRoleId.Admin => context.AccountId,
                EntityRoleId.Organization => context.OrganizationId,
                EntityRoleId.Manager => context.OrganizationId,
                EntityRoleId.User => context.OrganizationId,
                _ => (Guid?)null
            };

            if (!entityId.HasValue) return Array.Empty<IEntityMetadata>();

            return await _connection.Filter<EntityMetadata>()
                .Eq(x => x.EntityId, entityId.Value)
                .FindAsync();
        }

        public Task<IEnumerable<IEntityMetadata>> GetForEntityAsync(Guid organizationId)
            => GetAsync(new OrganizationContext(organizationId));

        public class EntityMetadata : IEntityMetadata
        {
            [BsonId]
            public ObjectId Id { get; set; }
            public Guid EntityId { get; set; }
            public Guid PartitionId { get; set; }
            public string Key { get; set; }
            public string Value { get; set; }
        }
    }
}