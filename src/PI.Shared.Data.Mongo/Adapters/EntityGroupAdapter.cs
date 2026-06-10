// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Crochik.Mongo;
// using PI.Shared.Data.Adapters;
// using PI.Shared.Models;

// namespace PI.Shared.Data.Mongo.Adapters
// {
//     public class EntityGroupAdapter : IEntityGroupAdapter
//     {
//         private readonly MongoConnection _connection;

//         public EntityGroupAdapter(MongoConnection connection)
//         {
//             this._connection = connection;
//         }

//         public async Task<IEntityGroup> CreateAsync(IEntityContext context, IEntityGroup group)
//         {
//             var dao = _connection.Map<EntityGroup>(group);
//             dao.Id = Guid.NewGuid();
//             dao.AccountId = context.AccountId.Value;
//             dao.EntityId = context.Role switch
//             {
//                 EntityRoleId.Account => context.AccountId.Value,
//                 EntityRoleId.Admin => context.AccountId.Value,
//                 EntityRoleId.Manager => context.OrganizationId.Value,
//                 EntityRoleId.Organization => context.OrganizationId.Value,
//                 _ => throw new Exception("Not authorized")
//             };

//             await _connection.InsertAsync(dao);

//             return dao;
//         }

//         public async Task<IEnumerable<IEntityGroup>> GetAsync(IEntityContext context)
//         {
//             var query = _connection.Filter<EntityGroup>();

//             switch (context.Role)
//             {
//                 case EntityRoleId.Account:
//                 case EntityRoleId.Admin:
//                     query.Eq(x => x.AccountId, context.AccountId);
//                     break;

//                 case EntityRoleId.Manager:
//                 case EntityRoleId.User:
//                 case EntityRoleId.Organization:
//                     query.Eq(x => x.AccountId, context.AccountId)
//                         .Eq(x => x.EntityId, context.OrganizationId.Value);
//                     break;

//                 default:
//                     return null;
//             }

//             return await query.FindAsync();
//         }

//         public async Task<IEntityGroup> GetByIdAsync(IEntityContext context, Guid id)
//         {
//             var query = _connection.Filter<EntityGroup>().Eq(x => x.Id, id);

//             if (context.Role != EntityRoleId.Root) query.Eq(x => x.AccountId, context.AccountId);

//             return await query.FirstOrDefaultAsync();
//         }
//     }
// }