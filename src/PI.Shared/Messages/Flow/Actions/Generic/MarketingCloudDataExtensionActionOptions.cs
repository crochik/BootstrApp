using System.Collections.Generic;

namespace Messages.Flow;

public class MarketingCloudDataExtensionActionOptions : ActionOptions 
{
    public const string UpsertedEventName = "UpsertedEvent";
    public const string FailedToUpsertEventName = "FailedToUpsertEvent";
    /// <summary>
    /// Data extension external key
    /// </summary>
    public string DataExtensionKey { get; set; }

    /// <summary>
    /// Field to use as the primary key
    /// </summary>
    public string PrimaryKeyField { get; set; }
    
    /// <summary>
    /// Primary key value
    /// </summary>
    public string PrimaryKey { get; set; }
    
    /// <summary>
    /// Other row values
    /// </summary>
    public Dictionary<string, string> Values { get; set; }
}