using System;
using PI.Shared.Models.Layout;

namespace PI.Shared.Models;

public class SaveDataViewRequest
{
    public DataViewRequest Request { get; set; }
    
    public string Name { get; set; }
    public string Description { get; set; }
    public ScreenBreakpoint? Breakpoint { get; set; }
    public EntityRoleId? Role { get; set; }
    public Guid[] ProfileIds { get; set; }
    public bool IsDefault { get; set; }
}