using System.Collections.Generic;

namespace PI.Shared.Models.Interfaces;

public interface IWithRelatedObjects
{
    // public List<KeyValuePair<string, object>> Refs { get; set; }
    // public Dictionary<string, object> Meta { get; set; }

    /// <summary>
    /// Referenced Objects
    /// Key: ObjectType full name (should it use safe ? with | instead of . ) 
    /// Value: ObjectId
    /// </summary>
    public Dictionary<string, object> RelatedObjects { get; set; }
}