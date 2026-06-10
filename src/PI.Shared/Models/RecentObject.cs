using Crochik.Mongo;

namespace PI.Shared.Models;

[BsonCollection("ObjectType.Recent")]
public class RecentObject : EntityOwnedModel
{
    public const string CollectionName = "ObjectType.Recent";
    
    public object ObjectId { get; set; }
    public string ObjectType { get; set; }
    public string[] AllObjectTypes { get; set; }
    public int Count { get; set; }
}