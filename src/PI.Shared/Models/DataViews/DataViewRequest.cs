using System.Collections.Generic;
using Crochik.Data;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Layout;

namespace PI.Shared.Models;

public enum GroupedFieldProjection
{
    Distinct, 
    First, 
    // Last,
    // Sum, 
    // Min, 
    // Max,
}

public class DataViewRequest : IQueryParams
{
    public Condition[] Criteria { get; set; }
    public int Top { get; set; }
    public string OrderBy { get; set; }
    public int Skip { get; set; }

    /// <summary>
    /// List of (ordered) fields to be returned
    /// </summary>
    public string[] Fields { get; set; }

    /// <summary>
    /// Accept header so we can generate the output in expected format (e.g. json or csv)
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// for lookups only 
    /// </summary>
    public string LookupField { get; set; }
    
    /// <summary>
    /// Fixed filtered views in this view 
    /// </summary>
    public string[] FixedFields { get; set; }
        
    /// <summary>
    /// hash to differentiate views that call the same Url
    /// e.g. a fixed filter view for an object vs a all-records view.
    /// </summary>
    private string _hash;
    public string Hash
    {
        get => string.IsNullOrWhiteSpace(_hash) ? null : _hash;
        set { _hash = value; }
    }
    
    /// <summary>
    /// (App)DataView name for object (optional)
    /// </summary>
    public string View { get; set; }
    
    /// <summary>
    /// Optional screen breakpoint from client
    /// </summary>
    public ScreenBreakpoint? Breakpoint { get; set; }
    
    /// <summary>
    /// EXPERIMENTAL (Optional)
    /// when specified will add group stage
    /// *** at least one field should be marked as "Distinct" *** 
    /// - for fields listed here, override projection for fields using
    /// - for all other Fields, will use $first 
    /// </summary>
    public Dictionary<string, GroupedFieldProjection> GroupedFields { get; set; }
}