using System;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.LangChain.Models;

[BsonCollection("ai.Prompt")]
public class ChatPrompt : AppProfileElement //, IFlowObject
{
    // public string ObjectType => "ai.Prompt";
    // public Guid? ObjectStatusId { get; set; }
    // public Guid? FlowId { get; set;  }
    
    /// <summary>
    /// Owner (Account, Organization or User)
    /// </summary>
    public Guid? EntityId { get; set; }
    
    public Guid AssistantId { get; set; }
    public Guid[] ChatStatuses { get; set; }
    public ChatMessage[] Messages { get; set; }
}