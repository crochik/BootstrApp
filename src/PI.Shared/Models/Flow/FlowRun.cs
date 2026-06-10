using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Messages.Flow;
using Newtonsoft.Json;

namespace PI.Shared.Models;

public class FlowRun : Model
{
    public string ObjectType { get; set; }
    public Dictionary<string, object> InitialObject { get; set; }
    public FlowEvent InitialEvent { get; set; }

    public Dictionary<string, ObjectWithType> Objects { get; set; }
    public RunStep[] Steps { get; set; }
    
    public FlowEvent[] FinalEvents { get; set; }

    public ExpandoObject BuildHandlebarsContext(FlowEvent evt = null, Action<Dictionary<string, object>> prepare = null)
    {
        var context = new Dictionary<string, object>
        {
            { nameof(InitialEvent), InitialEvent },
            { nameof(InitialObject), InitialObject },
            { nameof(Objects), Objects?.ToDictionary(x => x.Key, x => x.Value.Object) },
            { "Object", Objects[GetObjectAlias(ObjectType)].Object },
        };

        if (evt != null)
        {
            context.Add("Event", evt);
        }

        prepare?.Invoke(context);
        
        return JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(context));
    }

    public static string GetObjectAlias(string fullObjectName) => fullObjectName?.Replace('.', '|');
}