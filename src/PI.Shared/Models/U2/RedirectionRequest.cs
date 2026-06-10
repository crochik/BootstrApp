using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.U2;

[BsonCollection("u2.request")]
public class RedirectionRequest
{
    [BsonId] public ObjectId Id { get; set; } = ObjectId.GenerateNewId();
    public Guid RedirectionId { get; set; }
    public string Location { get; set; }
    public string UserAgent { get; set; }
    public string IpAddress { get; set; }
    public string RequestId { get; set; }
    public Dictionary<string,object> Query { get; set; }
    public string Url { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}