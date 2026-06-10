using System.Collections.Generic;
using System.Dynamic;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;

namespace PI.Shared.Services.ActionRunners;

public abstract class AbstractObjectRunner<T> : AbstractRunner<T>
    where T: ActionOptions, IActionOptionsForRunner
{
    protected readonly ILogger<AbstractObjectRunner<T>> _logger;
    protected readonly MongoConnection _connection;
    protected readonly ObjectTypeService _objectTypeService;
    
    protected AbstractObjectRunner(ILogger<AbstractObjectRunner<T>> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
    }    

    protected Dictionary<string, object> CalculateFields(ActionRunnerContext context, ExpandoObject runContext, Dictionary<string, object> mapping)
    {
        var updates = new Dictionary<string, object>();
        foreach (var kvp in mapping)
        {
            if (kvp.Value is not string str)
            {
                SetObjectValue(updates, kvp.Key, kvp.Value);
                continue;
            }

            if (!TryGet(context, runContext, str, out var value))
            {
                SetObjectValue(updates, kvp.Key, null);
                continue;
            }
            
            SetObjectValue(updates, kvp.Key, value);
        }

        return updates;
    }
    
    protected void SetObjectValue(Dictionary<string, object> updates, string path, object value)
    {
        var level = updates;
        var parts = path.Split('.');
        for (var c = 0; c < parts.Length - 1; c++)
        {
            if (!level.TryGetValue(parts[c], out var obj))
            {
                var newLevel = new Dictionary<string, object>();
                level.Add(parts[c], newLevel);
                level = newLevel;
                continue;
            }

            if (obj is not Dictionary<string, object> existing) throw new BadRequestException("Invalid path");
            level = existing;
        }
            
        level.Add(parts[^1], value);
    }
}