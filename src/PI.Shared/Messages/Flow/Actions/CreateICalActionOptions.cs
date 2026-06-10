using System;

namespace Messages.Flow;

public class CreateICalActionOptions : ActionOptions
{
    public Guid? NextEventId { get; set; }

    public override ActionOutput[] Output { get; set; }

    /// <summary>
    /// Default iCal summary (template)
    /// </summary>
    public string Summary { get; set; }    

    /// <summary>
    /// Default iCal description (template)
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// ical method 
    /// </summary>
    public string Method { get; set; }

    // add participants
    // ... 
    
    // target object
    // ...
}