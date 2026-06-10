using System.Collections.Generic;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace LMS.Models;

public class Context
{
    public Request Request { get; init; }
    public LeadType LeadType { get; set; }
    public Entity Entity { get; set; }
    public IDictionary<string, object> Object { get; set; }
    public Dictionary<string, object> Refs { get; set; }
    public HashSet<string> Tags { get; set; } = new() {"LMS"};
}