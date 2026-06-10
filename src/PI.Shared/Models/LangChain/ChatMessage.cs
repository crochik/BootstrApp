using System;
using System.Dynamic;

namespace PI.LangChain.Models;

public class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Text { get; set; }
    public string ContentType { get; set; }
    
    /// <summary>
    /// link to file 
    /// </summary>
    public Guid? RemoteFileId { get; set; }

    /// <summary>
    /// not in use yet?
    /// </summary>
    public ExecuteTool[] ToolCalls { get; set; }
    
    /// <summary>
    /// Thought Signature used by Gemini (TEST)
    /// </summary>
    public string ThoughtSignature { get; set; }
}

public class ExecuteTool
{
    public string Name { get; set; }
    public string Id { get; set; }
    public string Arguments { get; set; }
}