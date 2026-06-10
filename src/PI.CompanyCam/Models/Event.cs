using System;
using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.CompanyCam.Models;

[BsonCollection("companycam.Event")]
public class Event : EntityOwnedModel, IFlowObject
{
    public Dictionary<string, string> Headers { get; set; } = new();
    public IDictionary<string, object> Body { get; set; }

    public string ObjectType => "companycam.Event";
    public Guid? ObjectStatusId { get; set; }
    public Guid? FlowId { get; set; }
    public bool IsActive { get; set; } = true;
}