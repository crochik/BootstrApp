using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace PI.Shared.Extensions;

public static class DictionaryExtensions
{
    public static IDictionary<string, object> AddSubPath(this IDictionary<string, object> dict, string path, object value)
    {
        if (value == null)
        {
            return dict;
        }

        var parts = path.Split('.');
        if (parts.Length == 1)
        {
            dict[parts[0]] = value;
            return dict;
        }

        if (dict.TryGetValue(parts[0], out var obj))
        {
            if (obj is IDictionary<string, object> childdict)
            {
                childdict.AddSubPath(string.Join('.', parts, 1), value);
                return dict;
            }
            else
            {
                // TODO: go crazy, create an expando and copy all props
                // ...
                throw new NotImplementedException();
            }
        }

        var newdict = (IDictionary<string, object>)new ExpandoObject();
        dict[parts[0]] = newdict;
        newdict.AddSubPath(string.Join('.', parts, 1, parts.Length - 1), value);
        return dict;
    }

    public static object ResolveValue(this IDictionary<string, object> dict, params string[] path)
    {
        if (dict == null) return null;
        
        if (!dict.TryGetValue(path[0], out var value))
        {
            return null;
        }

        if (path.Length == 1)
        {
            return value;
        }

        if (value is not IDictionary<string, object> child)
        {
            if (value == null) return null;
            
            // special case of index in array
            if (value is IEnumerable<object> enumerable && int.TryParse(path[1], out var index))
            {
                var item = enumerable.Skip(index).FirstOrDefault();
                if (path.Length == 2) return item;
                child = (item as IDictionary<string, object>) ?? toDictionary(value);
                return child.ResolveValue(path[2..]);
            }
            
            child = toDictionary(value);
        }

        return child.ResolveValue(path[1..]);

        IDictionary<string, object> toDictionary(object parameters)
        {
            if (parameters == null) return null;
            if (parameters is IDictionary<string, object> dict) return dict;

            // BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance
            return parameters.GetType().GetProperties().ToDictionary
            (
                propInfo => propInfo.Name,
                propInfo => propInfo.GetValue(parameters, null)
            );
        }
    }

    public static bool TryGetParam(this IDictionary<string, object> dict, string name, out object value)
    {
        if (dict != null && dict.TryGetValue(name, out value)) return true;
        value = null;
        return false;
    }

    public static bool TryGetStrParam(this IDictionary<string, object> dict, string name, out string value)
    {
        value = null;
        if (!dict.TryGetParam(name, out var propValue)) return false;

        value = propValue as string ?? propValue?.ToString();
        return true;
    }

    /// <summary>
    /// Try get (Guid) value for key in dictionary
    ///     - If value exists, try to "convert" it into a Guid
    ///     - Will also recognize Mongo ObjectIds (string with 24 characters)  
    /// </summary>
    public static bool TryGetGuidParam(this IDictionary<string, object> dict, string key, out Guid id)
    {
        if (dict.TryGetParam(key, out var value) && value.TryParseGuid(out id)) return true;

        id = default;
        return false;
    }

    public static Guid? GetOptionalGuid(this IDictionary<string, object> dict, string name)
    {
        if (dict.TryGetGuidParam(name, out var id))
        {
            return id;
        }

        return default;
    }

    public static T GetOptional<T>(this IDictionary<string, object> dict, string name, T defaultValue = default)
        where T : class
    {
        if (dict.TryGetValue(name, out var value))
        {
            return (value as T) ?? defaultValue;
        }

        return defaultValue;
    }
}