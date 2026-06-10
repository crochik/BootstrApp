using System;
using System.Collections.Generic;
using System.Linq;
using Crochik.Extensions;
using MongoDB.Bson;
using PI.Shared.Exceptions;

namespace PI.Shared.Models;

public interface IEntityContext
{
    EntityRoleId Role { get; }
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
    Guid? AccountId { get; }

    Guid? ProfileId { get; }
    Guid[] AllProfileIds { get; }
    string ClientId { get; }

    Guid? EntityId { get; }
    IEnumerable<Guid> GetEntityIds();

    IReadOnlyDictionary<string, string[]> Claims { get; }
}

public class UserContext : IEntityContext
{
    public EntityRoleId Role { get; }
    public Guid? UserId { get; }
    public Guid? OrganizationId { get; }
    public Guid? AccountId { get; }
    public string Name { get; }

    public Guid? EntityId => UserId;

    public Guid? ProfileId { get; }
    public Guid[] AllProfileIds { get; }

    public string ClientId { get; }

    public IReadOnlyDictionary<string, string[]> Claims { get; }

    protected UserContext(
        Guid userId, string name, EntityRoleId role, Guid? organizationId, Guid? accountId,
        string clientId = default,
        Guid? profileId = default,
        IReadOnlyDictionary<string, string[]> claims = default,
        Guid[] otherProfileIds = default
    )
    {
        UserId = userId;
        Name = name;
        OrganizationId = organizationId;
        AccountId = accountId;
        Role = role;
        ClientId = clientId;
        Claims = claims;
        ProfileId = profileId;
        AllProfileIds = profileId.HasValue ? profileId.Value.AsEnumerable().Concat(otherProfileIds ?? Enumerable.Empty<Guid>()).ToArray() : [];
    }

    public static UserContext OrgUser(Guid userId, string name, EntityRoleId role, Guid organizationId, Guid? accountId,
        string clientId = default, Guid? profileId = default, IReadOnlyDictionary<string, string[]> claims = default, Guid[] otherProfileIds = default)
        => new(userId, name, role, organizationId, accountId, clientId, profileId, claims, otherProfileIds);

    public static UserContext Profile(Guid userId, string name, EntityRoleId role, Guid accountId, Guid? organizationId,
        string clientId = default, Guid? profileId = default, IReadOnlyDictionary<string, string[]> claims = default, Guid[] otherProfileIds = default)
        => new(userId, name, role, organizationId, accountId, clientId, profileId, claims,otherProfileIds);

    public static UserContext Admin(Guid userId, string name, Guid accountId, string clientId = default,
        Guid? profileId = default, IReadOnlyDictionary<string, string[]> claims = default, Guid[] otherProfileIds = default)
        => new(userId, name, EntityRoleId.Admin, null, accountId, clientId, profileId, claims, otherProfileIds);

    public IEnumerable<Guid> GetEntityIds()
    {
        yield return UserId.Value;
        if (OrganizationId.HasValue) yield return OrganizationId.Value;
        if (AccountId.HasValue) yield return AccountId.Value;
    }
}

/// <summary>
/// Arbitrary context
/// </summary>
public class ProfileContext : IEntityContext
{
    public EntityRoleId Role => EntityRoleId.Profile;

    public Guid? UserId { get; private init; }

    public Guid? OrganizationId { get; private init; }

    public Guid? AccountId { get; private init; }

    public Guid? ProfileId { get; private init; }
    public Guid[] AllProfileIds { get; private init; }

    public string ClientId { get; private init; }

    public Guid? EntityId => UserId ?? OrganizationId ?? AccountId;

    public IReadOnlyDictionary<string, string[]> Claims { get; private init; }

    public IEnumerable<Guid> GetEntityIds()
    {
        if (UserId.HasValue) yield return UserId.Value;
        if (OrganizationId.HasValue) yield return OrganizationId.Value;
        yield return AccountId.Value;
    }

    private ProfileContext()
    {
    }

    public static ProfileContext Create(Guid profileId, Guid accountId, Guid userId, string clientId,
        Guid? organizationId = null, IReadOnlyDictionary<string, string[]> claims = default, Guid[] otherProfileIds = default)
        => new()
        {
            AccountId = accountId,
            UserId = userId,
            ClientId = clientId,
            OrganizationId = organizationId,
            Claims = claims,
            ProfileId = profileId,
            AllProfileIds = profileId.AsEnumerable().Concat(otherProfileIds ?? Enumerable.Empty<Guid>()).ToArray(),
        };
}

public class RootContext : IEntityContext
{
    public EntityRoleId Role => EntityRoleId.Root;

    public Guid? UserId => null;

    public Guid? OrganizationId => null;

    public Guid? AccountId => null;

    public Guid? EntityId => null; // some other magic value?

    public Guid? ProfileId { get; init; }
    public Guid[] AllProfileIds { get; init;  }

    public string ClientId { get; init; }

    public IReadOnlyDictionary<string, string[]> Claims { get; init; }

    public IEnumerable<Guid> GetEntityIds()
    {
        yield break;
    }

    private RootContext()
    {
    }

    public static IEntityContext Context = new RootContext();
}

public class AccountContext : IEntityContext
{
    public EntityRoleId Role => EntityRoleId.Account;

    public Guid? UserId => null;

    public Guid? OrganizationId { get; }

    public Guid? AccountId { get; init; }

    public Guid? EntityId => AccountId;

    public Guid? ProfileId { get; init; }
    public Guid[] AllProfileIds { get; init;  }

    public string ClientId { get; init; }

    public IReadOnlyDictionary<string, string[]> Claims => null;

    public IEnumerable<Guid> GetEntityIds()
    {
        yield return AccountId.Value;
    }

    public AccountContext()
    {
    }

    public AccountContext(Guid accountId)
    {
        AccountId = accountId;
    }
}

public class OrganizationContext : IEntityContext
{
    public EntityRoleId Role => EntityRoleId.Organization;

    public Guid? UserId => null;

    public Guid? OrganizationId { get; init; }

    public Guid? AccountId { get; init; }

    public Guid? EntityId => OrganizationId;

    public Guid? ProfileId { get; init; }
    public Guid[] AllProfileIds { get; init; }

    public string ClientId { get; init; }

    public IReadOnlyDictionary<string, string[]> Claims => null;

    public OrganizationContext(Guid organizationId, Guid? accountId = null)
    {
        AccountId = accountId;
        OrganizationId = organizationId;
    }

    public IEnumerable<Guid> GetEntityIds()
    {
        yield return OrganizationId.Value;
        if (AccountId.HasValue) yield return AccountId.Value;
    }
}

public interface IContextWithActor : IEntityContext
{
    IEntityContext Original { get; }
    Actor Actor { get; }
}

public class ContextWithActor : IContextWithActor
{
    public IEntityContext Original { get; }
    public Actor Actor { get; }

    public virtual EntityRoleId Role => Original.Role;

    public virtual Guid? UserId => Original?.UserId;

    public virtual Guid? OrganizationId => Original?.OrganizationId;

    public virtual Guid? AccountId => Original?.AccountId;

    public virtual Guid? EntityId => Original?.EntityId;

    public Guid? ProfileId => Original.ProfileId;
    public Guid[] AllProfileIds => Original.AllProfileIds;

    public string ClientId => Original.ClientId;

    public IReadOnlyDictionary<string, string[]> Claims => Original.Claims;

    public virtual IEnumerable<Guid> GetEntityIds() => Original?.GetEntityIds();

    public ContextWithActor(Actor actor, IEntityContext context = null)
    {
        Actor = actor;
        Original = context;
    }
}

[Obsolete]
public class IntegrationContext : IEntityContext
{
    public EntityRoleId Role => EntityRoleId.Integration;

    public Guid? UserId => null;

    public Guid? OrganizationId => null;

    public Guid? AccountId => null;

    public Guid IntegrationId { get; }

    public Guid? EntityId => null;

    public Guid? ProfileId => null;
    public Guid[] AllProfileIds => [];

    public string ClientId => null;

    public IReadOnlyDictionary<string, string[]> Claims => null;

    public IntegrationContext(Guid integrationId)
    {
        IntegrationId = integrationId;
    }

    public IEnumerable<Guid> GetEntityIds() => Enumerable.Empty<Guid>();
}

public class ContactContext : IEntityContext
{
    public EntityRoleId Role => EntityRoleId.Contact;
    public Guid? UserId => null;
    public Guid? OrganizationId => null;
    public Guid? AccountId => null;
    public Guid? EntityId => null;

    public Guid? ProfileId => null;
    public Guid[] AllProfileIds => [];

    public string ClientId => null;

    public IEnumerable<Guid> GetEntityIds() => Enumerable.Empty<Guid>();
    public IReadOnlyDictionary<string, string[]> Claims => null;

    public ContactContext()
    {
    }
}

public static class IEntityContextExtensions
{
    public static IContextWithActor WithContext(this Actor actor, IEntityContext context) =>
        new ContextWithActor(actor, context);

    public static IContextWithActor With(this IEntityContext context, Actor actor) =>
        new ContextWithActor(actor, context);

    public static IEntityContext WithActorFrom(this IEntityContext context, IEntityContext other) =>
        other.Actor() != null ? new ContextWithActor(other.Actor(), context) : context;

    public static Actor Actor(this IEntityContext context) =>
        (context as IContextWithActor)?.Actor ?? PI.Shared.Models.Actor.Current;

    // TODO: add support for Contact (Lead)
    [Obsolete("should use object type configuration instead")]
    public static bool CanAccess(this IEntityContext context, IEntityContext other)
    {
        switch (context.Role)
        {
            case EntityRoleId.Root:
                return true;

            case EntityRoleId.Admin:
            case EntityRoleId.Account:
            {
                switch (other.Role)
                {
                    case EntityRoleId.Admin:
                    // return context.Role == EntityRoleId.Account ||
                    //     (context.UserId.HasValue && context.UserId.Value == other.UserId.Value);

                    case EntityRoleId.Account:
                    case EntityRoleId.Organization:
                    case EntityRoleId.Manager:
                    case EntityRoleId.User:
                    case EntityRoleId.Disabled:
                        return context.AccountId == other.AccountId;
                }

                break;
            }
            case EntityRoleId.Organization:
            case EntityRoleId.Manager:
            {
                switch (other.Role)
                {
                    case EntityRoleId.Manager:
                    // return context.Role == EntityRoleId.Organization ||
                    //     (context.UserId.HasValue && context.UserId.Value == other.UserId.Value);

                    case EntityRoleId.User:
                    case EntityRoleId.Disabled:
                    case EntityRoleId.Organization:
                        return context.OrganizationId == other.OrganizationId;
                }

                break;
            }

            case EntityRoleId.Profile:
            case EntityRoleId.User:
                return context.UserId == other.UserId;
        }

        return false;
    }

    public static Guid GetOwnerEntityId(this IEntityContext context)
        => context.Role switch
        {
            EntityRoleId.Admin => context.AccountId.Value,
            EntityRoleId.Account => context.AccountId.Value,
            EntityRoleId.Manager => context.OrganizationId.Value,
            EntityRoleId.Organization => context.OrganizationId.Value,
            EntityRoleId.User => context.EntityId.Value,

            _ => throw new ForbiddenException(context)
        };

    /// <summary>
    /// Elevate context to org or account
    /// </summary>
    public static IEntityContext GetOwnerEntityContext(this IEntityContext context)
        => context.Role switch
        {
            EntityRoleId.Account => context,
            EntityRoleId.Admin => new AccountContext(context.AccountId.Value)
            {
                ClientId = context.ClientId,
            }.WithActorFrom(context),
            EntityRoleId.Organization => context,
            EntityRoleId.Manager or EntityRoleId.User => new OrganizationContext(context.OrganizationId.Value, context.AccountId.Value)
            {
                ClientId = context.ClientId,
            },

            _ => throw new ForbiddenException(context)
        };

    /// <summary>
    /// Derive context from current to impersonate role/profile
    /// </summary>
    public static IEntityContext DeriveUserContext(this IEntityContext context, EntityRoleId? entityRoleId = null, Guid? profileId = null)
    {
        if (context.Role != EntityRoleId.Admin)
        {
            // for now limit to admins
            throw new ForbiddenException("Only Admins");
        }

        var result = entityRoleId switch
        {
            EntityRoleId.Admin => context,
            EntityRoleId.Manager or EntityRoleId.User => UserContext.OrgUser(Guid.Empty, "User", entityRoleId.Value, Guid.Empty, context.AccountId.Value, context.ClientId),
            null => profileId.HasValue ? ProfileContext.Create(profileId.Value, context.AccountId.Value, Guid.Empty, context.ClientId, Guid.Empty) : throw new BadRequestException("Missing Profile"),
            _ => throw new BadRequestException("Invalid Role")
        };

        return result;
    }

    private static IEnumerable<Guid> GetAllIds(this IEntityContext context, EntityRoleId? entityRoleId)
    {
        switch (entityRoleId)
        {
            case EntityRoleId.User:
                if (context.UserId.HasValue) yield return context.UserId.Value;
                break;
            case EntityRoleId.Organization:
                if (context.OrganizationId.HasValue) yield return context.OrganizationId.Value;
                break;
            case null:
                if (context.UserId.HasValue) yield return context.UserId.Value;
                if (context.OrganizationId.HasValue) yield return context.OrganizationId.Value;
                if (context.AccountId.HasValue) yield return context.AccountId.Value;
                break;
        }
        
        if (!context.Claims.TryGetValue("pi_ghost", out var ghosts)) yield break;

        foreach (var ghost in ghosts)
        {
            var ids = ghost.Split(":").Select(Guid.Parse).ToArray();
            switch (entityRoleId)
            {
                case EntityRoleId.User:
                    yield return ids[0];
                    break;
                case EntityRoleId.Organization:
                    yield return ids[1];
                    break;
                case null:
                    yield return ids[0];
                    yield return ids[1];
                    break;
            }
        }
    }

    public static Guid[] GetAllUserIds(this IEntityContext context) => context.GetAllIds(EntityRoleId.User).ToArray();
    public static Guid[] GetAllOrganizationIds(this IEntityContext context) => context.GetAllIds(EntityRoleId.Organization).ToArray();
    public static Guid[] GetAllEntityIds(this IEntityContext context) => context.GetAllIds(null).ToArray();

    // does not handle AllProfileIds
    [Obsolete("try to use ExpressionEvaluatorService instead")]
    public static Dictionary<string, object> GetPlaceholders(this IEntityContext context)
    {
        return new Dictionary<string, object>(getTokens());

        IEnumerable<KeyValuePair<string, object>> getTokens()
        {
            yield return new KeyValuePair<string, object>("AccountId", context.AccountId.Value.AsSerializedId());
            yield return new KeyValuePair<string, object>("OrganizationId", context.Role switch
            {
                // the organization for "an account/admin" is the account itself
                EntityRoleId.Account => context.AccountId.AsSerializedId(),
                EntityRoleId.Admin => context.AccountId.AsSerializedId(),
                // users 
                EntityRoleId.Manager => context.OrganizationId.Value.AsSerializedId(),
                EntityRoleId.Organization => context.OrganizationId.Value.AsSerializedId(),
                EntityRoleId.User => context.OrganizationId.Value.AsSerializedId(),
                // others
                _ => null,
            });
            if (context.UserId.HasValue)
                yield return new KeyValuePair<string, object>("UserId", context.UserId.Value.AsSerializedId());
            if (context.EntityId.HasValue)
                yield return new KeyValuePair<string, object>("EntityId", context.EntityId.Value.AsSerializedId());
            if (context.ProfileId.HasValue)
                yield return new KeyValuePair<string, object>("ProfileId", context.ProfileId.Value.AsSerializedId());
            yield return new KeyValuePair<string, object>("Actor", context.Actor());
            yield return new KeyValuePair<string, object>("new Date", DateTime.UtcNow);
            yield return new KeyValuePair<string, object>("new UUID", Model.NewGuid());
            yield return new KeyValuePair<string, object>("new ObjectId", Model.NewObjectId());
            yield return new KeyValuePair<string, object>("NULL", null);
        }
    }

    [Obsolete("use ExpressionEvaluatorService")]
    public static bool DeprecatedTryResolveExpression(this IEntityContext context, string str, out object resolved)
    {
        if (!str.StartsWith("{{") || !str.EndsWith("}}"))
        {
            resolved = str;
            return true;
        }

        resolved = str switch
        {
            "{{AccountId}}" or "{{context \"AccountId\"}}" => context.AccountId.Value.AsSerializedId(),
            "{{OrganizationId}}" or "{{context \"OrganizationId\"}}" => context.Role switch
            {
                // the organization for "an account/admin" is the account itself
                EntityRoleId.Account => context.AccountId.AsSerializedId(),
                EntityRoleId.Admin => context.AccountId.AsSerializedId(),
                // users 
                EntityRoleId.Manager => context.OrganizationId.Value.AsSerializedId(),
                EntityRoleId.Organization => context.OrganizationId.Value.AsSerializedId(),
                EntityRoleId.User => context.OrganizationId.Value.AsSerializedId(),
                // others
                _ => throw new ForbiddenException(context, "Not associated with organization"),
            },
            "{{UserId}}" or "{{context \"UserId\"}}" => context.UserId.Value.AsSerializedId(),
            "{{EntityId}}" or "{{context \"EntityId\"}}" => context.EntityId.Value.AsSerializedId(),
            "{{Actor}}" or "{{context \"Actor\"}}" => context.Actor(),
            "{{new Date}}" => DateTime.UtcNow,
            "{{new UUID}}" => Model.NewGuid(),
            "{{new ObjectId}}" => Model.NewObjectId(),
            "{{NULL}}" => null,
            "{{TRUE}}" => true,
            "{{FALSE}}" => false,
            // "{{OwnerEntityId}}" => context.GetOwnerEntityId().AsSerializedId(),
            _ => null,
        };

        return resolved != null || str == "{{NULL}}";
    }
}