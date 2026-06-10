namespace PI.Shared.Models;

/// <summary>
/// Lookup Settings to be used when looking up "Object Type"
/// - assumes the same criteria (constraints) from the object type
/// </summary>
public class LookupFields
{
    /// <summary>
    /// Name of the field to be used as the key, by default will be _id;
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Nome of the field to be used as the "Label"
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Nome of the field to be used as the "Description"  
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// template using field names to build the Url for the image
    /// </summary>
    public string ImageUrl { get; set; }
}