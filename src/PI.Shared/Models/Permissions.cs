using System;
using System.Collections.Generic;
using System.Reflection;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Exceptions;

namespace PI.Shared.Models;

[Flags]
public enum Permission
{
    None = 0,
    Read = 1,
    Update = 2,
    Create = 4,
    Delete = 8,
    All = 15,
}

public enum AccessLevel
{
    Owner,
    Group,
    Guest,
    None,
}

public class RBAC<T>
    where T : Enum
{
    [BsonElement] public Dictionary<string, T> Permissions { get; init; } = new();

    public T this[EntityRoleId roleId]
    {
        get => Permissions.TryGetValue(roleId.ToString(), out var permission)
            ? permission
            : roleId switch
            {
                EntityRoleId.Account => this[EntityRoleId.Admin],
                EntityRoleId.Organization => this[EntityRoleId.Manager],
                _ => default(T),
            };
        set => Permissions[roleId.ToString()] = value;
    }

    public T this[Guid entityOrProfileId]
    {
        get => Permissions.TryGetValue(entityOrProfileId.ToString(), out var permission) ? permission : default(T);
        set => Permissions[entityOrProfileId.ToString()] = value;
    }

    public T this[IEntityContext context]
    {
        get
        {
            // by profile, if provided wins
            if (context.ProfileId.HasValue)
            {
                if (context.AllProfileIds.Length > 1)
                {
                    foreach (var profileId in context.AllProfileIds)
                    {
                        if (Permissions.TryGetValue(profileId.ToString(), out var profilePermission)) return profilePermission;    
                    }   
                }
                else
                {
                    if (Permissions.TryGetValue(context.ProfileId.ToString(), out var profilePermission)) return profilePermission;
                }
            }

            // by user, if provided wins (09/2023)
            if (context.UserId.HasValue && Permissions.TryGetValue(context.UserId.ToString(), out var permission)) return permission;

            // by org, if provided wins (09/2023)
            if (context.OrganizationId.HasValue && Permissions.TryGetValue(context.OrganizationId.ToString(), out permission)) return permission;

            // fallback to role
            return this[context.Role];
        }
    }

    public void Add(EntityRoleId role, T permission) => this[role] = permission;

    public RBAC<T> Set(EntityRoleId role, T permisions)
    {
        this[role] = permisions;
        return this;
    }

    public bool Can(IEntityContext context, T permission) => this[context].HasFlag(permission);

    public bool IsEmpty() => Permissions == null || Permissions.IsEmpty();
}

[Flags]
public enum FieldPermission
{
    None = 0,
    Read = 1,
    Update = 2,
    SetOnCreate = 4, // be set by user as part of the creation form
    Reset = 8, // whether after being set it can be unset (to allow for it not be required but to not be removed after was set) 
    CreateOnDemand = 16, // for related objects, whether it can be created on demand 
}

public class FieldRBAC : RBAC<FieldPermission>
{
}

public static class FieldRBACExtensions
{
    public static bool CanRead(this FieldRBAC rbac, IEntityContext context) => rbac?.Can(context, FieldPermission.Read) ?? false;
    public static bool CanUpdate(this FieldRBAC rbac, IEntityContext context) => rbac?.Can(context, FieldPermission.Update) ?? false;
    public static bool CanReset(this FieldRBAC rbac, IEntityContext context) => rbac?.Can(context, FieldPermission.Reset) ?? false;
    public static bool CanSetOnCreate(this FieldRBAC rbac, IEntityContext context) => rbac?.Can(context, FieldPermission.SetOnCreate) ?? false;
    public static bool CanCreateOnDemand(this FieldRBAC rbac, IEntityContext context) => rbac?.Can(context, FieldPermission.CreateOnDemand) ?? false;
}