using System;

namespace PI.Shared.Models;

[Flags]
public enum RelatedObjectTypePermission
{
    None = 0,
    Read = 1,
    // Update = 2,
    // SetOnCreate = 4, // be set by user as part of the creation form
    // Reset = 8, // whether after being set it can be unset (to allow for it not be required but to not be removed after was set) 
    // CreateOnDemand = 16, // for related objects, whether it can be created on demand 
}

public static class RelatedObjectTypeRBACExtensions
{
    public static bool CanRead(this RelatedObjectTypeRBAC rbac, IEntityContext context) => rbac?.Can(context, RelatedObjectTypePermission.Read) ?? false;
}

public class RelatedObjectTypeRBAC : RBAC<RelatedObjectTypePermission>
{
}