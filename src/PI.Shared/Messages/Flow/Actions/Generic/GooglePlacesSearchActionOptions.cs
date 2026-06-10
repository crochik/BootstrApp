namespace Messages.Flow;

public class GooglePlacesSearchActionOptions : ActionOptions
{
    public const string OnFoundEvent = nameof(OnFoundEvent);
    
    public string NewObjectType { get; set; } = "google.Place";
    public string Alias { get; set; }
    
    /// <summary>
    /// Search Criteria (expression)
    /// </summary>
    public string SearchText { get; set; }
}