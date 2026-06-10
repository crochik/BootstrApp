using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.OpenAPI;

[BsonCollection("openapi.Schema")]
public class Schema : IModel
{
    [BsonElement] public Guid Id => ObjectType.Id;

    [BsonElement] public Guid AccountId => ObjectType.AccountId;

    [BsonElement] public string Name => ObjectType.Name;
    [BsonElement] public string FullName => ObjectType.FullName;

    [BsonElement] public string Namespace => ObjectType.Namespace;

    public ObjectType ObjectType { get; set; }

    public string Reference { get; set; }
    public Dictionary<string, object> Raw { get; set; }

    public Schema(ObjectType objectType)
    {
        ObjectType = objectType;
    }
}