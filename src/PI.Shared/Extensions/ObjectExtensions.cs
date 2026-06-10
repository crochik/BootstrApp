using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using MongoDB.Bson;

namespace PI.Shared.Extensions;

public static class ObjectExtensions
{
    public static bool TryParseGuid(this object value, out Guid id)
    {
        if (value == null)
        {
            id = default;
            return false;
        }

        if (value is Guid native)
        {
            id = native;
            return true;
        }

        if (Guid.TryParse(value.ToString(), out id)) return true;
        if (ObjectId.TryParse(value.ToString(), out var objectId))
        {
            id = objectId.ToGuid();
            return true;
        }

        id = default;
        return false;
    }

    public static Dictionary<string, object> GetPropertiesAsDictionary(this object obj)
    {
        var dict = new Dictionary<string, object>();

        if (obj is IEnumerable<KeyValuePair<string, object>> e)
        {
            foreach (var item in e)
            {
                dict.TryAdd(item.Key, item.Value);
            }

            return dict;
        }

        if (obj != null)
        {
            foreach (var prop in obj.GetType().GetProperties())
            {
                var value = prop.GetValue(obj);
                if (value == null) continue;
                dict.Add(prop.Name, value);
            }
        }

        return dict;
    }

    public static IEnumerable<KeyValuePair<string, object>> FlattenAllProperties(this object obj, string prefix = null)
    {
        if (obj == null) yield break;

        if (obj is IDictionary<string, object> dict)
        {
            foreach (var kv in dict)
            {
                var results = FlattenAllProperties(kv.Value, prefix == null ? kv.Key : $"{prefix}|{kv.Key}");
                foreach (var result in results)
                {
                    yield return result;
                }
            }
            yield break;
        }

        if (obj is IEnumerable<object> list)
        {
            var c = 0;
            foreach (var i in list)
            {
                var results = FlattenAllProperties(i, $"{prefix}|{c++}");
                foreach (var result in results)
                {
                    yield return result;
                }
            }
            yield break;
        }

        yield return new KeyValuePair<string, object>(prefix, obj);
    }

    public static Dictionary<string, object> ToDictionaryObject(this ExpandoObject expando)
    {
        var result = new Dictionary<string, object>();
        var dict = (IDictionary<string, object>)expando;
        foreach (var kvp in dict)
        {
            var value = kvp.Value switch
            {
                ExpandoObject expandoObject => expandoObject.ToDictionaryObject(),
                _ => kvp.Value
            };
            
            result.Add(kvp.Key, value);
        }

        return result;
    } 
}