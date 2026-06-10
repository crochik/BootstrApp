using System;
using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.Openphone.Models;

[BsonCollection("openphone.Contact")]
public class OpenPhoneContact : EntityOwnedModel, IExternalId, IFlowObject
{
    public IDictionary<string,object> Properties { get; set; }
    public string ExternalId { get; set; }
    
    public string NormalizedPhoneNumber { get; set; }
    
    public string NormalizedEmail { get; set; }

    public string ObjectType => "openphone.Contact";
    
    public Guid? ObjectStatusId { get; set; }
    public Guid? FlowId { get; set;  }
    public bool IsActive { get; set; } = true;
    
    // public ObjectStatusMilestones ObjectStatusMilestones { get; set; }
}