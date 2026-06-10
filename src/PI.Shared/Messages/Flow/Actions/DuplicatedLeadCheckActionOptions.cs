using System;
using PI.Shared.Constants;

namespace Messages.Flow;

public class DuplicatedLeadCheckActionOptions : ActionOptions
{
    public const string TAG_DUPLICATE = "Duplicate";
    public const string TAG_SUPPRESSED = "Suppresed";

    /// <summary>
    /// how far back to look, null means default for now
    /// only implemented on the runner as now
    /// </summary>
    public TimeSpan? Offset { get; set; }
    
    /// <summary>
    /// Always fire the next event (even when there is duplicate)
    /// only implemented on the runner as now
    /// </summary>
    public bool AlwaysFireNextEvent { get; set; }
    
    public Guid? NextEventId { get; set; }
    public Guid? DuplicateLeadEventId { get; set; }
    public Guid? OriginalLeadEventId { get; set; }
    
    public Guid? ErrorEventId { get; set; }
    
    public override ActionOutput[] Output { get; set; }
}

public class LeadDupeCheckAction : FlowAction<DuplicatedLeadCheckActionOptions, LeadDupeCheckAction.Message>
{
    public override Guid Id => ActionIds.DuplicatedLeadCheck;

    public class Message : SimpleActionMessage<DuplicatedLeadCheckActionOptions>
    {
        public Message() { }
        public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
    }
}