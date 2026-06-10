using System.Collections.Generic;

namespace PI.Shared.Models.Interfaces;

public interface IWithMultiplePlayers 
{
    /// <summary>
    /// Key: "Role"
    /// Value: EntityId 
    /// </summary>
    public Dictionary<string, object> Players { get; set; }
}