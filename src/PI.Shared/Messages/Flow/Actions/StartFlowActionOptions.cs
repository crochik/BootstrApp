using System;

namespace Messages.Flow;

public class StartFlowActionOptions : ActionOptions
{
    /// <summary>
    /// Field Name to set (by default FlowId) 
    /// </summary>
    public string FieldName { get; set; }
    
    /// <summary>
    /// Flow to be initiated
    /// </summary>
    public Guid FlowId { get; set; }
    
    /// <summary>
    /// Event to fire if flow is set 
    /// </summary>
    public Guid? NextEventId { get; set; }
    
    /// <summary>
    /// Event to fire when the object is already assigned the flow
    /// </summary>
    public Guid? AlreadyRunningEventId { get; set; }
    
    public override ActionOutput[] Output { get; set; }
}