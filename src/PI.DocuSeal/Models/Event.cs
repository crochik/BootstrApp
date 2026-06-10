using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;
using PI.Shared.Models.Interfaces;

namespace PI.DocuSeal.Models;

[BsonCollection("docuseal.Event")]
public class Event : EntityOwnedModel, IFlowObject
{
    [BsonIgnore]
    public string ObjectType => "docuseal.Event";

    public BsonDocument Body { get; set; }

    public Guid? SubmissionId { get; set; }
    public ReferencedObject? Parent { get; set; }

    public Guid? ObjectStatusId { get; init; }
    public Guid? FlowId { get; init; }
    public bool IsActive { get; init; } = true;
}