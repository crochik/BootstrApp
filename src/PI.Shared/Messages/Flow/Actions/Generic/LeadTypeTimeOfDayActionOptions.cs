namespace Messages.Flow;

/// <summary>
/// Auto tag and fail if out the constraints
/// </summary>
public class LeadTypeTimeOfDayActionOptions : ActionOptions, IActionOptionsForRunner
{
    /// <summary>
    /// Path to date property with a date
    /// </summary>
    public string DatePath { get; set; }
    
    /// <summary>
    /// can be a path to a variable or the actual timezone
    /// </summary>
    public string TimeZoneId { get; set; }
}