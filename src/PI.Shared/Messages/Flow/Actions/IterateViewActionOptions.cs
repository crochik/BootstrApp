using System;

namespace Messages.Flow;

public class IterateViewActionOptions : ActionOptions
{
    public override ActionOutput[] Output { get; set; }
    
    /// <summary>
    /// id or expression (or name?) 
    /// </summary>
    public string AppDataView { get; set; }
    
    // additional criteria?
    //...
    
    public Guid? NextEventId { get; set; }
}