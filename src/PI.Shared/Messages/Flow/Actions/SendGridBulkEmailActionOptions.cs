using System;

namespace Messages.Flow;

public class SendGridBulkEmailActionOptions : ActionOptions
{
    public override ActionOutput[] Output { get; set; }
    
    /// <summary>
    /// (optional) BCC - not template! 
    /// </summary>
    public string BCC { get; set; }

    public Guid? NextEventId { get; set; }
    public Guid? ErrorEventId { get; set; }
}