namespace PI.Shared.Services;

public class ReferenceValue
{
    public string Id { get; set; }

    /// <summary>
    /// Probably should have been called "Label" (or at least Name) for consistency
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Description to be used
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Thumbnail to be used 
    /// </summary>
    public string ImageUrl { get; set; }
}