using System.Collections.Generic;
using Messages.Flow;

namespace PI.LangChain.Models;

public class GenerativeAICompletionActionOptions : ActionOptions
{
    public const string OnCompletionEvent = "OnCompletionEvent";
    
    /// <summary>
    /// User Message (can be an expression)
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
}