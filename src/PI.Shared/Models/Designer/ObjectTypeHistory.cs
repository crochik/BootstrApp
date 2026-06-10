using Crochik.Mongo;

namespace PI.Shared.Models.Designer;

/// <summary>
/// Object Type change log
/// </summary>
[BsonCollection("ObjectType.History")]
public class ObjectTypeHistory : EntityOwnedModel
{
    public ObjectType Before { get; set; }
    public ObjectType After { get; set; }
}