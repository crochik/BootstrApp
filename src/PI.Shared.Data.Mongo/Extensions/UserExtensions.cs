using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Constants;

namespace PI.Shared.Models;

public static class UserExtensions
{
    public static Query<User> UserQuery(this MongoConnection connection, Guid accountId, Guid userId)
        => connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, accountId)
            .Eq(x => x.Id, userId)
    ;

    public static Query<User> UserQuery(this MongoConnection connection, IEntityContext context)
        => connection.UserQuery(context.AccountId.Value, context.UserId.Value);
}