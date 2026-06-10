using System;

namespace PI.Shared.Models;

[Flags]
public enum ObjectTypePermission
{
    None = 0,
    Read = 1,
    Update = 2,
    Create = 4,
    Delete = 8,
    Import = 16,
    Export = 32,
    BulkDelete = 64,
    BulkTag = 128,
    Customize = 256,
    DeepClone = 512,
}

public class ObjectTypeRBAC : RBAC<ObjectTypePermission>
{
}