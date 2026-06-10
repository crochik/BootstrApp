using System;
using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.CompanyCam.Models;

[BsonCollection("companycam.Project")]
public class CCProject : EntityOwnedModel, IFlowObject
{
    public string ExternalId { get; set; }
    public IDictionary<string, object> Properties { get; set; }
    public string ObjectType { get; set; }
    public Guid? ObjectStatusId { get; set; }
    public Guid? FlowId { get; set;  }
    public bool IsActive { get; set; }
}