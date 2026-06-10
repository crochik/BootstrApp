using System;
using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.Openphone.Models;

[BsonCollection("openphone.Event")]
public class OpenPhoneEvent : EntityOwnedModel, IExternalId, IFlowObject
{
    public Dictionary<string, string> Headers { get; set; } = new();
    public OpenPhoneRawEvent Event { get; set; }
    public string ExternalId { get; set; }

    /// <summary>
    /// National 
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Normalized Phone number of the contact
    /// </summary>
    public string NormalizedPhoneNumber { get; set; }

    public string ObjectType { get; set; }
    public Guid? ObjectStatusId { get; set; }
    public Guid? FlowId { get; set; }
    public bool IsActive { get; set; }
    // public ObjectStatusMilestones ObjectStatusMilestones { get; set; }
}