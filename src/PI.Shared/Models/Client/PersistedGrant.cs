using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.Client;

[BsonCollection("idp.PersistedGrant")]
public class PersistedGrant 
{
    [BsonId]
    public string Key { get; set; }
    public string Type { get; set; }
    public string SubjectId { get; set; }
        
    /// <summary>
    /// Gets the client identifier.
    /// </summary>
    /// <value>
    /// The client identifier.
    /// </value>
    public string ClientId { get; set; }
        
    public DateTime CreationTime { get; set; }
    public DateTime? Expiration { get; set; }
    public string Data { get; set; }
        
    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    /// <value>
    /// The session identifier.
    /// </value>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets the description the user assigned to the device being authorized.
    /// </summary>
    /// <value>
    /// The description.
    /// </value>
    public string Description { get; set; }
        
    /// <summary>
    /// Gets or sets the consumed time.
    /// </summary>
    /// <value>
    /// The consumed time.
    /// </value>
    public DateTime? ConsumedTime { get; set; }        
}