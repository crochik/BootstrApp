using System;
using System.Collections.Generic;
using Crochik.Mongo;
using Messages.Flow;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

public class KeyValue<T>
{
    [BsonElement("k")]
    public string Key { get; set; }

    [BsonElement("v")]
    [BsonSerializer(typeof(CustomObjectSerializer))]
    public T Value { get; set; }

    public KeyValue() { }
    public KeyValue(string key, T value)
    {
        Key = key;
        Value = value;
    }
}

public class KeyValue : KeyValue<object>
{
    public KeyValue() { }
    public KeyValue(string key, object value) : base(key,value) {}
}

[BsonCollection("FlowLog")]
public class FlowEventLog
{
    [BsonId]
    public ObjectId Id { get; set; }
    public Guid AccountId { get; set; }

    public string ObjectType { get; set; }

    [BsonSerializer(typeof(MagicGuidSerializer))]
    public Guid ObjectId { get; set; }

    public Guid FlowId { get; set; }
    public Guid? StatusId { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public string Type { get; set; }
    public string Description { get; set; }
    public Guid EventId { get; set; }
    public Guid RunId { get; set; }
    public string Action { get; set; }
    public Actor Actor { get; set; }
    public KeyValue[] Refs { get; set; }
    public Dictionary<string, object> Meta { get; set; }

    public FlowEvent Event { get; set; }
    
    [BsonIgnoreIfDefault]
    public bool Failed { get; set; }
}