using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;

namespace PI.Shared.Models;

/// <summary>
/// Element access is controlled by Profile and/or Role
/// </summary>
public interface IProfileElement : IModel
{
    /// <summary>
    /// (optional) Limit to these profiles
    /// </summary>
    Guid[] ProfileIds { get; set; }

    /// <summary>
    /// (optional) Limit to this role
    /// with additional fallbacks from Account to Admin an Organization to Manager
    /// </summary>
    EntityRoleId? Role { get; set; }

    bool IsActive { get; set; }

    DateTime CreatedOn { get; set; }
    DateTime? LastModifiedOn { get; set; }
    Actor LastActor { get; set; }
}

public interface IObjectTypeProfileElement : IProfileElement
{
    string ObjectType { get; set; }
}

public static class IProfileElementExtensions
{
    public static Task<T> GetProfileElementAsync<T>(this MongoConnection connection, IEntityContext context, string name)
        where T : IProfileElement
    {
        return connection.GetProfileElementAsync<T>(context, q => q.Eq(x => x.Name, name));
    }

    public static async Task<T> GetProfileElementAsync<T>(this MongoConnection connection, IEntityContext context, Action<Query<T>> additionalCriteria = null)
        where T : IProfileElement
    {
        var query = connection.GetProfileElementQuery(context, additionalCriteria);

        var list = await query.FindAsync();
        return context.PickFromProfileElements(list);
    }

    public static async Task<IEnumerable<T>> GetProfileElementsAsync<T, TKey>(this MongoConnection connection, IEntityContext context, Func<T, TKey> groupBy, Action<Query<T>> additionalCriteria = null)
        where T : IProfileElement
    {
        var list = await connection.GetProfileElementsAsync<T>(context, additionalCriteria);

        return list.GroupBy(groupBy)
                .Select(x => context.PickFromProfileElements<T>(x.ToArray()))
                .Where(x => x != null)
            ;
    }

    public static async Task<List<T>> GetProfileElementsAsync<T>(this MongoConnection connection, IEntityContext context, Action<Query<T>> additionalCriteria = null)
        where T : IProfileElement
    {
        var query = connection.GetProfileElementQuery(context, additionalCriteria);

        return await query.FindAsync();
    }

    private static T PickFromProfileElements<T>(this IEntityContext context, IList<T> options)
        where T : IProfileElement
    {
        if (options.Count < 1) return default;
        if (options.Count == 1) return options[0];

        var candidates = options.Any(x => x.AccountId == context.AccountId.Value) ? options.Where(x => x.AccountId == context.AccountId).ToArray() : options.ToArray();

        if (candidates.Length == 1) return candidates[0];

        // exact profile
        var result = default(T);

        if (context.ProfileId.HasValue)
        {
            if (context.AllProfileIds.Length > 1)
            {
                foreach (var profileId in context.AllProfileIds)
                {
                    result = candidates.FirstOrDefault(x => x.ProfileIds != null && x.ProfileIds.Any(p => p == profileId));
                    if (result != null) break;
                }
            }
            else
            {
                result = candidates.FirstOrDefault(x => x.ProfileIds != null && x.ProfileIds.Any(p => p == context.ProfileId));
            }
        }

        // exact role
        result ??= candidates.FirstOrDefault(x => x.ProfileIds == null && x.Role == context.Role);

        if (result == null)
        {
            // fallback roles
            switch (context.Role)
            {
                case EntityRoleId.Account:
                    result = candidates.FirstOrDefault(x => x.ProfileIds == null && x.Role == EntityRoleId.Admin);
                    break;

                case EntityRoleId.Organization:
                    result = candidates.FirstOrDefault(x => x.ProfileIds == null && x.Role == EntityRoleId.Manager);
                    break;
            }
        }

        return result;
    }

    public static Query<T> GetProfileElementQuery<T>(this MongoConnection connection, IEntityContext context, Action<Query<T>> additionalCriteria = null)
        where T : IProfileElement
    {
        var query = connection.Filter<T>()
                .In(x => x.AccountId, [context.AccountId.Value, Guid.Empty])
                .In(x => x.Role, getRoles())
                .Ne(x => x.IsActive, false)
            ;

        additionalCriteria?.Invoke(query);

        if (context.ProfileId.HasValue)
        {
            if (context.AllProfileIds.Length > 1)
            {
                query.OrBuilder(
                    q => q.Eq(x => x.ProfileIds, null),
                    q => q.AnyIn(x => x.ProfileIds, context.AllProfileIds)
                );
            }
            else
            {
                query.OrBuilder(
                    q => q.Eq(x => x.ProfileIds, null),
                    q => q.AnyEq(x => x.ProfileIds, context.ProfileId.Value)
                );
            }
        }
        else
        {
            query.Exists(x => x.ProfileIds, false);
        }

        return query;

        IEnumerable<EntityRoleId?> getRoles()
        {
            // not role based
            yield return null;

            // exact role
            yield return context.Role;

            // automatic fallback from 
            switch (context.Role)
            {
                case EntityRoleId.Account:
                    yield return EntityRoleId.Admin;
                    break;

                case EntityRoleId.Organization:
                    yield return EntityRoleId.Manager;
                    break;
            }
        }
    }

    public static bool CanAccess(this IEntityContext context, IProfileElement profileElement)
    {
        if (profileElement == null) return false;
        if (context.Role == EntityRoleId.Root) return true;
        if (context.AccountId.Value != profileElement.AccountId) return false;
        if (profileElement.ProfileIds != null)
        {
            // constrained by profile
            if (context.AllProfileIds.Length > 1)
            {
                return context.ProfileId.HasValue && profileElement.ProfileIds.ContainsAny(context.AllProfileIds);
            }
            
            return context.ProfileId.HasValue && profileElement.ProfileIds.Contains(context.ProfileId.Value);
        }

        // fallback to role(s)
        return profileElement.Role.HasValue && context.Role switch
        {
            EntityRoleId.Account => profileElement.Role.Value == context.Role || profileElement.Role.Value == EntityRoleId.Admin,
            EntityRoleId.Organization => profileElement.Role.Value == context.Role || profileElement.Role.Value == EntityRoleId.Manager,
            _ => profileElement.Role.Value == context.Role,
        };
    }
}