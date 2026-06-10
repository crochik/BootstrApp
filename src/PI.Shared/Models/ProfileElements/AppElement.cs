using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

public abstract class AppElement : IModel
{
    [BsonId]
    [BsonSerializer(typeof(MagicGuidSerializer))]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonSerializer(typeof(MagicGuidSerializer))]
    public Guid AccountId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedOn { get; set; }
    public Actor LastActor { get; set; }
    public bool IsActive { get; set; } = true;
}