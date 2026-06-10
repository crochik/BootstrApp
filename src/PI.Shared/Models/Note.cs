using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Models.Interfaces;

namespace PI.Shared.Models;

[Obsolete]
[JsonConverter(typeof(StringEnumConverter))]
public enum ContentFormat
{
    PlainText,
    Html,
    Markdown,
}

[BsonKnownTypes(typeof(EmbeddedContent), typeof(LinkedContent))]
[BsonDiscriminator(Required = true)]
[DiscriminatorWithFallback]
public class AbstractContent
{
    public string ContentType { get; set; }
    public int? Size { get; set; }
}

[BsonDiscriminator("embed")]
public class EmbeddedContent : AbstractContent
{
    public string Content { get; set; }
}

[BsonDiscriminator("link")]
public class LinkedContent : AbstractContent
{
    public string Uri { get; set; }
}

[DiscriminatorWithFallback]
[BsonDiscriminator(Required = true)]
[BsonCollection("Note")]
[BsonKnownTypes(
    typeof(CommunicationNote)
)]
public class Note : FlowObjectModel // , INote
{
    public List<KeyValuePair<string, object>> Refs { get; set; }
    public Dictionary<string, object> Meta { get; set; }
    
    /// <summary>
    /// mime content type
    /// </summary>
    public string ContentType { get; set; }
    
    public string Content { get; set; }
    
    /// <summary>
    /// Named attachments
    /// </summary>
    public Dictionary<string, AbstractContent> Attachments { get; set; }
    
    public string Provider { get; set; }
    public string ExternalId { get; set; }

    /// <summary>
    /// Link to note in external system if one exists
    /// </summary>
    public string ExternalUrl { get; set; }

    // new generic fields: not in use yet
    // public ReferencedObject Parent { get; set; }
    // public Dictionary<string, object> RelatedObjects { get; set; }
    // public Guid? CreatorId { get; set; }
}