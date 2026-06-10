using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;
using PI.Shared.Models.Files;

namespace PI.LangChain.Models;

public enum ChatStatus
{
    New,
    Ready, 
    Generating,
    Error,
}

public enum ModelThinkingLevel
{
    Low, 
    Medium, 
    High,
}

[BsonDiscriminator]
[DiscriminatorWithFallback]
[BsonKnownTypes(
    typeof(OpenAIModelProperties),
    typeof(GeminiModelProperties)
    )
]
public class ModelProperties
{
    public string Model { get; set; }
    public bool? ImageCapability { get; set; }
    public ModelThinkingLevel? ThinkingLevel { get; set; }
    
    // public int? Seed { get; set; }
    // public int? Temperature { get; set; }
}

[BsonDiscriminator("OpenAI")]
public class OpenAIModelProperties: ModelProperties
{
}

[BsonDiscriminator("Gemini")]
public class GeminiModelProperties : ModelProperties
{
}

[BsonCollection("ai.Assistant")]
public class Assistant : EntityOwnedModel, IProfileElement
{
    public Guid IntegrationId { get; set; }
    
    [Obsolete("Use Model Properties instead")]
    public string Model { get; set; }
    
    public ModelProperties ModelProperties { get; set; }

    /// <summary>
    /// When set it will present this message to the user while waiting for
    /// first user message 
    /// </summary>
    public string WelcomeMessage { get; set; }
    
    public string SystemPrompt { get; set; }
    public Dictionary<string, string> Inputs { get; set; }

    /// <summary>
    /// Content type to be generated
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Response format to inform the model
    /// </summary>
    public string ResponseFormat { get; set; }

    /// <summary>
    /// when defined it will use structured output (openai) 
    /// </summary>
    public string ObjectType { get; set; }
    
    /// <summary>
    /// Assistant flow
    /// </summary>
    public Guid? FlowId { get; set; }

    /// <summary>
    /// assistant object status
    /// </summary>
    public Guid? ObjectStatusId { get; set; }

    /// <summary>
    /// new chat flow 
    /// </summary>
    public Guid? ChatFlowId { get; set; }

    /// <summary>
    /// new chat object status id
    /// </summary>
    [Obsolete("use ChatStatuses?")]
    public Guid? ChatObjectStatusId { get; set; }

    /// <summary>
    /// Chat statuses mapping
    /// </summary>
    public Dictionary<string, Guid> ChatStatuses { get; set; }

    public Guid[] ProfileIds { get; set; }
    public EntityRoleId? Role { get; set; }
    public bool IsActive { get; set; }

    /// <summary>
    /// stored procedures used to generate system prompt
    /// </summary>
    public Dictionary<string, string> StoredProcedures { get; set; }

    /// <summary>
    /// Define where files used by the assistant should be created
    /// </summary>
    public UploadFileOptions  UploadFileOptions { get; set; }
    // other files???
    // ...

    public Guid? MapChatStatus(ChatStatus status) => ChatStatuses != null && ChatStatuses.TryGetValue(status.ToString(), out var statusId) ? statusId : null;
    
    /// <summary>
    /// status to be applied to new chats 
    /// </summary>
    /// <returns></returns>
    public Guid? MapNewChatStatus() => MapChatStatus(ChatStatus.New) ?? ChatObjectStatusId;
}