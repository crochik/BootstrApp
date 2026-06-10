using System;
using System.Collections.Generic;
using Crochik.Mongo;

namespace PI.Shared.Models;

[BsonCollection("job.ImportObjects")]
public class ImportObjectsJob : FlowObjectModel
{
    public Dictionary<string, object> Mapping { get; set; }
    public Guid SourceRemoteFileId { get; set; }
    public Guid? OutputRemoteFileId { get; set; }
    public string TargetObjectType { get; set; }
    public DateTime? StartedOn { get; set; }
    public DateTime? EndedOn { get; set; }
}