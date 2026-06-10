using System;
using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.LangChain.Models;

public enum ChatRole
{
    System,
    Assistant,
    User,
    Tool
}

public class EmbeddedContent
{
    public string ContentType { get; set; }
    public string Content { get; set; }
}

[BsonCollection("ai.Chat")]
public class Chat : EntityOwnedModel, IFlowObject
{
    public string ObjectType => "ai.Chat";

    public Assistant Assistant { get; set; }
    public ChatMessage[] Messages { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    /// <summary>
    /// chat flow
    /// </summary>
    public Guid? FlowId { get; set; }

    /// <summary>
    /// chat object status
    /// </summary>
    public Guid? ObjectStatusId { get; set; }

    public bool IsActive { get; set; } = true;

    public IDictionary<string, object> MetaValues { get; set; }
    
    /// <summary>
    /// Embedded content in the last completion
    /// </summary>
    public EmbeddedContent EmbeddedContent { get; set; }

    // public List<KeyValuePair<string, object>> RefValues { get; set; }

    /// <summary>
    /// Map object status id to "ChatStatus" using assistant configuration
    /// </summary>
    public ChatStatus? GetStatus()
    {
        if (Assistant?.ChatStatuses == null) return null;
        foreach (ChatStatus stt in Enum.GetValues(typeof(ChatStatus)))
        {
            if (Assistant.ChatStatuses.TryGetValue(stt.ToString(), out var statusId) && statusId == ObjectStatusId) return stt;
        }

        return null;
    }
}