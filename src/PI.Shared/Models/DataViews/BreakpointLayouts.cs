using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Form.Models;

namespace PI.Shared.Models;

public class BreakpointLayouts
{
    [JsonProperty("xs")]
    public FormLayout ExtraSmall { get; set; }    
    
    [JsonProperty("sm")]
    public FormLayout Small { get; set; }    
    
    [JsonProperty("md")]
    public FormLayout Medium { get; set; }    
    
    [JsonProperty("lg")]
    public FormLayout Large { get; set; }    
    
    [JsonProperty("xl")]
    public FormLayout ExtraLarge { get; set; }

    [JsonIgnore]
    [BsonIgnore]
    public IEnumerable<FormLayout> All
    {
        get
        {
            if (ExtraSmall != null) yield return ExtraSmall;
            if (Small != null) yield return Small;
            if (Medium != null) yield return Medium;
            if (Large != null) yield return Large;
            if (ExtraLarge != null) yield return ExtraLarge;
        }
    }
}