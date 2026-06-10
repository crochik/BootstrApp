using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.Dashboards;

[BsonDiscriminator(Required = true)]
[BsonKnownTypes(
    typeof(MongoDbLoadSource)
)]
public abstract class LoadSource
{
    public int? Order { get; set; }
}