using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

[BsonCollection("lms.Transaction")]
public class Transaction
{
    public const string ObjectTypeName = "LMSTransaction";
    
    [BsonId] public Guid Id { get; set; }

    public Guid? AccountId { get; set; }
    public Guid? EntityId { get; set; }
    
    public Guid? FlowId { get; set; }
    public Guid? ObjectStatusId { get; set; }

    public Request Request { get; set; }
    public Response Response { get; set; }

    public IDictionary<string, object> ParsedInput { get; set; }
    public decimal? AcceptedCost { get; set; }
    public decimal? RejectedCost { get; set; }

    public IDictionary<string, object> Refs { get; set; }
    
    public string[] Tags { get; set; }
    
    public string Message { get; set; }
}