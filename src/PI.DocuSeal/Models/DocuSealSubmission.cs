using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;
using PI.Shared.Models.Interfaces;

namespace PI.DocuSeal.Models;

[BsonCollection("docuseal.Submission")]
public class DocuSealSubmission : EntityOwnedModel, IFlowObject, IWithParent, IWithAuthor, IWithContent
{
    public const string ObjectTypeFullName = "docuseal.Submission";
    
    [BsonIgnore]
    public string ObjectType => ObjectTypeFullName;

    
    public Guid? ObjectStatusId { get; set; }
    public Guid? FlowId { get; set; }
    public bool IsActive { get; set; }

    public Guid TemplateId { get; set; }

    public ReferencedObject? Parent { get; set; }
    public Guid? CreatorId { get; set; }
    public string? ContentType { get; set; }
    public string? Content { get; set; }

    /// <summary>
    /// DocuSeal Submission Id 
    /// </summary>
    public int? ExternalId { get; set; }

    public DocuSealSubmitter[]? Submitters { get; set; }
}