using System;
using Crochik.Dipper;
using Crochik.Mongo;
using PI.Shared.Form.Models;
using PI.Shared.Models.Layout;

namespace PI.Shared.Models;

[BsonCollection("app.DataView.1")]
public class AppDataView : AppProfileElement, IDataView, IFlowObject, IObjectTypeProfileElement
{
    public const string ObjectTypeFullName = "AppDataView";
    
    /// <summary>
    /// Use stored procedure (optional)
    /// If missing, requires object Type 
    /// </summary>
    public AggregateStoredProcedure StoredProcedure { get; set; }

    /// <summary>
    /// Data View
    /// </summary>
    public DataView DataView { get; set; }

    /// <summary>
    /// If no stored procedure is provided
    /// use object Type to define it 
    /// </summary>
    public string ObjectType { get; set; }

    /// <summary>
    /// Saved criteria for the view
    /// </summary>
    public Criteria Criteria { get; set; }

    public string[] Fields { get; set; }

    public string OrderBy { get; set; }

    /// <summary>
    /// Options
    /// </summary>
    public DataViewOptions Options { get; set; }

    public Guid? FlowId { get; set; }
    public Guid? ObjectStatusId { get; set; }
    
    /// <summary>
    /// When set it will be only available for a given breakpoint
    /// </summary>
    public ScreenBreakpoint? Breakpoint { get; set; }
    
    /// <summary>
    /// Hash for the view (e.g. for fixed fields, subview, ...)
    /// </summary>
    public string Hash { get; set; }
    
    /// <summary>
    /// Whether this view is a default for the object (+breakpoint, +hash, +profile/role)
    /// </summary>
    public bool IsDefault { get; set; }

    
}