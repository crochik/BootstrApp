using System;
using System.Collections.Generic;

namespace Messages.Flow;

public class GenerativeAICompletionActionOptions : ActionOptions
{
    public const string OnCompletionEvent = "OnCompletionEvent";
    public const string OnFailedEvent = "OnFailedEvent";

    /// <summary>
    /// (Additional) System Message (can be a template)
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// User Message (can be a template)
    /// </summary>
    public string UserMessage { get; set; }

    /// <summary>
    /// Assistant (can be an expression or the id of an existing assistant) 
    /// </summary>
    public string AssistantId { get; set; }
    
    /// <summary>
    /// Overridden Input Values 
    /// </summary>
    public Dictionary<string, string> Inputs { get; set; }    
    
    /// <summary>
    /// Alias to be used to add the result to flowRun so it can be used 
    /// </summary>
    public string Alias { get; set; }
    
    /// <summary>
    /// if the assistant generates json, when set a new object will be created for this type
    /// </summary>
    public string NewObjectType { get; set; }
    
    /// <summary>
    /// If a new object is created, assign this flow
    /// </summary>
    public Guid? NewObjectFlowId { get; set; }
}