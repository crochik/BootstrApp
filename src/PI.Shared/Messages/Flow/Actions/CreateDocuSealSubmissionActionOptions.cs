using System.Collections.Generic;

namespace Messages.Flow;

public class CreateDocuSealSubmissionActionOptions : ActionOptions//, IGenericActionBuilder
{
    public const string SuccessEventName = "SubmissionCreated";
    public const string FailedEvent = "FailedToCreateSubmission";
    
    public string Name { get; set; }
    public string ObjectType { get; set; }
    public string ObjectId { get; set; }
    public string TemplateId { get; set; }
    public string SubmitterName { get; set; }
    public string SubmitterEmail { get; set; }
    public string SubmitterRole { get; set; }
    
    public Dictionary<string, string> Inputs { get; set; }
    
    /// <summary>
    /// Creator/"Sender"
    /// - expression to resolve to user
    /// - if null, will try to resolve the {{InitialEvent.Actor.UserId}}
    /// </summary>
    public string CreatorId { get; set; }
}