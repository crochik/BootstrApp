using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class MappedNewModelAdapter<TInt, TObj> : MappedModelAdapter<TInt, TObj>
        where TObj : class, TInt, IEntityOwnedModel
        where TInt : IRow<Guid>
    {
        public MappedNewModelAdapter(MongoConnection connection) :
            base(connection)
        {
        }

        public override async Task<IEnumerable<TInt>> GetTrunkAsync(IEntityContext context)
        {
            return await Connection.Filter<TObj>()
                .In(x => x.EntityId, GetEntityIds(context))
                .FindAsync();
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
}