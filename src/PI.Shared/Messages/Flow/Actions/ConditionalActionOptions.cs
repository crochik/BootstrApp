using System;
using PI.Shared.Models;

namespace Messages.Flow;

public class ConditionalActionOptions : ActionOptions
{
    public Criteria Criteria { get; set; }
    public Guid? TrueEventId { get; set; }
    public Guid? FalseEventId { get; set; }
}

public class SwitchCase : Criteria
{
    public string Name { get; set; }
    public Guid EventId { get; set; }
}

public class SwitchActionOptions : ActionOptions
{
    public SwitchCase[] Cases { get; set; }

    public string DefaultCase { get; set; }
    public Guid DefaultEventId { get; set; } 
}