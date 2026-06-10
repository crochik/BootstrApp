using System;
using System.Collections.Generic;
using PI.Shared.Extensions;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Requests;

public interface IDataFormActionRequest
{
    /// <summary>
    /// Action
    /// </summary>
    string Action { get; set; }

    /// <summary>
    /// Id of objects selected 
    /// </summary>
    Guid[] SelectedIds { get; set; }

    /// <summary>
    /// (optional) (App)DataView name for object 
    /// </summary>
    string View { get; set; }
}

public class DataFormActionRequest<T> : IDataFormActionRequest
{
    /// <summary>
    /// Action
    /// </summary>
    public string Action { get; set; }
        
    /// <summary>
    /// Additional parameters
    /// </summary>
    public T Parameters { get; set; }
        
    /// <summary>
    /// Id of objects selected 
    /// </summary>
    public Guid[] SelectedIds { get; set; }
        
    /// <summary>
    /// (optional) (App)DataView name for object 
    /// </summary>
    public string View { get; set; }
}

public class DataFormActionRequest : DataFormActionRequest<Dictionary<string, object>> 
{
    public bool TryGetParam(string name, out object value) => Parameters.TryGetParam(name, out value);
    public bool TryGetStrParam(string name, out string value) => Parameters.TryGetStrParam(name, out value);
    public bool TryGetGuidParam(string name, out Guid value) => Parameters.TryGetGuidParam(name, out value);
}

public class DataViewActionRequest : DataFormActionRequest
{
    /// <summary>
    /// (optional) current (app)dataView criteria when applicable  
    /// </summary>
    public Condition[] Criteria { get; set; }
    
    /// <summary>
    /// (optional) current List of (ordered) fields in the (app)dataView when applicable
    /// </summary>
    public string[] Fields { get; set; }

    /// (optional) current sort order in the (app)dataView when applicable
    public string OrderBy { get; set; }
}

public class DataFormActionResponse
{
    public string Action { get; init; }
    public Guid[] Ids { get; init; }
    public bool Success { get; init; }
    public string Message { get; set; }
    public string NextUrl { get; init; }
    
    /// <summary>
    /// Id of the run initiated by the action 
    /// </summary>
    public Guid? RunId { get; init; }

    public DataFormActionResponse() { }

    public DataFormActionResponse(IDataFormActionRequest request, string message = null, bool success = false)
    {
        Action = request.Action;
        Ids = request.SelectedIds;
        Message = message;
        Success = success;
    }
    
    public static DataFormActionResponse Error(IDataFormActionRequest request, string message) => new DataFormActionResponse(request, message);
}

public class UserActionRequest : DataFormActionRequest
{
    public Guid EventId { get; set; }
    public string ObjectType { get; set; }
}