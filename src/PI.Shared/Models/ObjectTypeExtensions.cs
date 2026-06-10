namespace PI.Shared.Models;

public static class ObjectTypeExtensions
{
    public static bool CanRead(this ObjectType ot, IEntityContext context) => ot.Can(context, ObjectTypePermission.Read);
    public static bool CanUpdate(this ObjectType ot, IEntityContext context) => ot.Can(context, ObjectTypePermission.Update);
    public static bool CanDelete(this ObjectType ot, IEntityContext context) => ot.Can(context, ObjectTypePermission.Delete);
    public static bool CanCreate(this ObjectType ot, IEntityContext context) => ot.Can(context, ObjectTypePermission.Create);
    public static bool Can(this ObjectType ot, IEntityContext context, ObjectTypePermission permission) => ot?.RBAC?.Can(context, permission) ?? false;
}

