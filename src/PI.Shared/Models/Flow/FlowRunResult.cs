using System;
using System.Collections.Generic;
using Crochik.Mongo;

namespace PI.Shared.Models;

/// <summary>
/// result of a flow run ... 
/// </summary>
[BsonCollection("FlowRunResult")]
public class FlowRunResult : EntityOwnedModel
{
    public bool? Success { get; set; }
    public string ErrorMessage { get; set; }
    public IDictionary<string, object> Result { get; set; }
    public string ResultType { get; set; }
    public Guid FlowRunId { get; set; }
}