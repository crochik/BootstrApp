using System.Collections.Generic;

namespace PI.Shared.Models.OpenAPI;

public class Path // : EntityOwnedModel
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Reference { get; set; }
    public Dictionary<string, object> Raw { get; set; }

    public Dictionary<string, Operation> Operations { get; set; }
}