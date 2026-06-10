using System;

namespace Messages.Flow;

public class TakeSnapshotActionOptions : ActionOptions
{
    public override ActionOutput[] Output { get; set; }
    public Guid NextEventId { get; set; }
}