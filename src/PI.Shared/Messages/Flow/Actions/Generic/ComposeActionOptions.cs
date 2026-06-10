namespace Messages.Flow;

public class ComposeActionOptions : ActionOptions, IActionOptionsForRunner
{
    public const string ContentCreatedEventName = "ContentCreated";
    public const string FailedToCreateContentEventName = "FailedToCreateContent";
 
    /// <summary>
    /// handlesbars template
    /// </summary>
    public string Template { get; set; }
    
    /// <summary>
    /// Content type of the result genereated 
    /// TODO: if application/json we could convert it into an object?
    /// ....
    /// </summary>
    public string ContentType { get; set; }
    
    /// <summary>
    /// Alias to be used for the result
    /// It will be saved in the event.MetaValues.[Alias] 
    /// </summary>
    public string Alias { get; set; }
}