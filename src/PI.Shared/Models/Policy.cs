// using System;
// using System.Collections.Generic;
// using System.Linq;
//
// namespace PI.Shared.Models;
//
// public class Policy
// {
//     private static readonly Dictionary<Type, Dictionary<EntityRoleId, Policy>> Cache = new();
//
//     public static Policy Default = new(
//         EntityRoleId.Admin,
//         Permission.All,
//         Permission.Read,
//         Permission.None
//     );
//
//     public static Dictionary<EntityRoleId, Policy> Get<T>() where T : IEntityOwnedModel
//     {
//         if (!Cache.TryGetValue(typeof(T), out var dict))
//         {
//             dict = typeof(T).GetCustomAttributes(typeof(PolicyAttribute), true)
//                 .OfType<PolicyAttribute>()
//                 .ToDictionary(x => x.Level, x => x.Policy);
//
//             Cache.TryAdd(typeof(T), dict);
//         }
//
//         return dict;
//     }
//
//     private readonly Permission[] _permissions;
//     public Permission this[AccessLevel level] => _permissions[(int)level];
//     public EntityRoleId Owner { get; }
//
//     public Policy(EntityRoleId role, Permission owner, Permission group, Permission guest)
//     {
//         Owner = role;
//
//         _permissions = new Permission[] {
//             owner,
//             group,
//             guest,
//             Permission.None
//         };
//     }
// }


// public static class IEntityContextPermissionsExtensions
// {
    // public static AccessLevel EvaluateRelationShip(this IEntityContext context, IEntityOwnedModel model)
    // {
    //     if (context.Role == EntityRoleId.Root) return AccessLevel.Owner;
    //     if (context.AccountId.Value != model.AccountId) return AccessLevel.None;
    //
    //     if (context.AccountId.Value == model.EntityId)
    //     {
    //         // owned by account
    //         return context.Role switch
    //         {
    //             EntityRoleId.Admin => AccessLevel.Owner,
    //             EntityRoleId.Account => AccessLevel.Owner,
    //             EntityRoleId.Manager => AccessLevel.Group,
    //             EntityRoleId.Organization => AccessLevel.Group,
    //             _ => AccessLevel.Guest
    //         };
    //     }
    //
    //     if (context.OrganizationId.HasValue && model.EntityId == context.OrganizationId.Value)
    //     {
    //         // owned by org
    //         return context.Role switch
    //         {
    //             EntityRoleId.Organization => AccessLevel.Owner,
    //             EntityRoleId.Manager => AccessLevel.Owner,
    //             EntityRoleId.User => AccessLevel.Group,
    //             _ => throw new ForbiddenException(context)
    //         };
    //     }
    //
    //     if (context.UserId.HasValue && model.EntityId == context.UserId.Value)
    //     {
    //         // user is owner
    //         return context.Role switch
    //         {
    //             EntityRoleId.Admin => AccessLevel.Owner,
    //             EntityRoleId.Manager => AccessLevel.Owner,
    //             EntityRoleId.User => AccessLevel.Owner,
    //             _ => throw new ForbiddenException(context)
    //         };
    //     }
    //
    //     // can't determine ownership
    //     return context.Role switch
    //     {
    //         EntityRoleId.Admin => AccessLevel.Owner,
    //         EntityRoleId.Account => AccessLevel.Owner,
    //         _ => AccessLevel.Guest
    //     };
    // }

    // public static bool TryGetPolicy<T>(this IEntityContext context, out Policy policy) where T : IEntityOwnedModel
    // {
    //     var key = context.Role switch
    //     {
    //         EntityRoleId.Account => EntityRoleId.Account,
    //         EntityRoleId.Admin => EntityRoleId.Account,
    //         EntityRoleId.Organization => EntityRoleId.Organization,
    //         EntityRoleId.Manager => EntityRoleId.Organization,
    //         EntityRoleId.User => EntityRoleId.User,
    //         _ => EntityRoleId.Disabled
    //     };
    //
    //     return Policy.Get<T>().TryGetValue(key, out policy);
    // }

    // public static bool CanCreate<T>(this IEntityContext context) where T : IEntityOwnedModel
    //     => context.TryGetPolicy<T>(out _);
    //
    // public static T Create<T>(this IEntityContext context) where T : EntityOwnedModel, new()
    // {
    //     if (context.TryGetPolicy<T>(out var policy))
    //     {
    //         var obj = new T
    //         {
    //             Id = typeof(T).GetCustomAttribute<UseObjectIdAttribute>() != null ? Model.NewObjectId() : Model.NewGuid(),
    //             AccountId = context.AccountId.Value,
    //             EntityId = policy.Owner switch
    //             {
    //                 EntityRoleId.Admin when context.Role == EntityRoleId.Admin => context.UserId.Value,
    //                 EntityRoleId.Account => context.AccountId.Value,
    //                 EntityRoleId.Manager when context.Role == EntityRoleId.Manager => context.UserId.Value,
    //                 EntityRoleId.Organization => context.OrganizationId.Value,
    //                 EntityRoleId.User => context.UserId.Value,
    //                 _ => throw new ForbiddenException(context, "Role can't create objects of this type") // ????
    //             },
    //             CreatedOn = DateTime.UtcNow,
    //             LastModifiedOn = DateTime.UtcNow,
    //             LastActor = Actor.Current,
    //         };
    //
    //         return obj;
    //     }
    //
    //     throw new ForbiddenException(context, $"Can't create {typeof(T).Name}");
    // }
// }