using System;
using PI.Shared.Salesforce.Models;

namespace PI.Salesforce.Models;

public class InstallationMap
{
    public Guid Id { get; set; }
    public WorkOrder WorkOrder { get; set; }
    public SfOption Option { get; set; }

    public SfOptionLineItem[] OptionLineItems { get; set; }
    public Section[] Sections { get; set; }
    public MaterialAssignment[] MaterialAssignments { get; set; }
    public Room[] Rooms { get; set; }
    public SfExternalLink[] ExternalLinks { get; set; }
    public Room[] OtherRooms { get; set; }
}