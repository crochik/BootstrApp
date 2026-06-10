using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Diff;

public class SimpleDiffer
{
    private static Dictionary<Type, Func<object, object, string, DiffResult>> DefaultMapping = new()
    {
        { typeof(String), (o, n, p) => string.Equals((string)o, (string)n) ? null : DiffResult.New((string)o, (string)n, p) },
        { typeof(Guid), (o, n, p) => (Guid)o == (Guid)n ? null : DiffResult.New((Guid)o, (Guid)n, p) }
    };

    public static DiffResult Diff<T>(T oldValue, T newValue, SimpleDiffOptions options = null, string path = null)
    {
        if (oldValue == null)
        {
            return newValue == null ? null : DiffResult.New(oldValue, newValue, path);
        }

        if (newValue == null) return DiffResult.New(oldValue, newValue, path);

        if (options?.Mapping?.TryGetValue(oldValue.GetType(), out var typeDiff) ?? false)
        {
            return typeDiff.Invoke(oldValue, newValue, path);
        }

        if (DefaultMapping.TryGetValue(oldValue.GetType(), out typeDiff))
        {
            return typeDiff.Invoke(oldValue, newValue, path);
        }

        var oldType = oldValue.GetType();
        var newType = newValue.GetType();
        if (oldType != newType)
        {
            // for now, if it is a different type, it is a full change
            // TODO: handle some automatic conversions and or deep compares?
            // ...
            return DiffResult.New(oldValue, newValue, path);
        }
        
        if (oldValue is IDictionary oldDictionary && newValue is IDictionary newDictionary)
        {
            return CompareDicts(oldType, oldDictionary, newDictionary, options, path);
        }

        if (oldValue is IList oldList && newValue is IList newList)
        {
            return CompareArrays(oldType, oldList, newList, options, path);
        }
        
        if (oldType.IsArray)
        {
            throw new NotImplementedException($"{path}.({oldType.Name}): array not supported type");
        }

        if (oldType.IsGenericType)
        {
            throw new NotImplementedException($"{path}.({oldType.Name}): generics not supported type");
        }

        if (oldType.IsPrimitive)
        {
            return oldValue.Equals(newValue) ? null : DiffResult.New(oldValue, newValue, path);
        }

        if (oldValue is IComparable oldComparable)
        {
            return oldComparable.CompareTo(newValue) == 0 ? null : DiffResult.New(oldValue, newValue, path);
        }
        
        if (!options?.Visited.Add(oldValue) ?? false)
        {
            // already visited 
            return null;
        }

        var properties = oldType.GetProperties();
        // if (properties.Length == 0)
        // {
        //     throw new NotImplementedException($"{oldType.FullName} not supported type");
        // }
        
        var delta = propertiesDiff().Where(x => x != null).OrderBy(x => x.Path).ToArray();
        if (delta.Length == 0) return null;

        return new ObjectDiffResult
        {
            OldValue = oldType,
            NewValue = newValue,
            Path = path,
            Diffs = delta,
        };

        IEnumerable<DiffResult> propertiesDiff()
        {
            foreach (var prop in properties)
            {
                if (!prop.CanRead) continue;
                if (prop.GetMethod?.IsStatic ?? false) continue;

                // indexed property []
                if (prop.GetIndexParameters().Length > 0) continue;

                if (options?.SkipJsonIgnore ?? false)
                {
                    // if (Attribute.IsDefined(prop, typeof(JsonIgnoreAttribute))) continue;

                    if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                }

                if (options?.SkipBsonIgnore ?? false)
                {
                    // if (Attribute.IsDefined(prop, typeof(BsonIgnoreAttribute))) continue;
                    if (prop.GetCustomAttribute<BsonIgnoreAttribute>() != null) continue;
                }

                if (options?.SkipSimpleDiffIgnore ?? false)
                {
                    // if (Attribute.IsDefined(prop, typeof(SimpleDiffIgnoreAttribute))) continue;
                    
                    if (prop.GetCustomAttribute<SimpleDiffIgnoreAttribute>() != null) continue;
                }

                var ignore = options?.ExcludeProperty?.Invoke(oldType, prop) ?? false;
                if (ignore) continue;
                
                var oldPropValue = prop.GetValue(oldValue);
                var newPropValue = prop.GetValue(newValue);
                var diff = Diff(oldPropValue, newPropValue, options, prop.Name);
                yield return diff;
            }
        }
    }

    private static DiffResult CompareArrays(Type propertyType, IList oldList, IList newList, SimpleDiffOptions options, string path)
    {
        var delta = diff().Where(x => x != null).ToArray();
        if (delta.Length == 0) return null;

        if (oldList.Count != newList.Count)
        {
            // replace entire array?
            return DiffResult.New(oldList, newList, path);
        }
        
        return new ArrayDiffResult
        {
            OldValue = oldList,
            NewValue = newList,
            Path = path,
            Diffs = delta,
        };

        IEnumerable<DiffResult> diff()
        {
            var length = int.Max(oldList.Count, newList.Count);

            for (var c = 0; c < length; c++)
            {
                var oldValue = c < oldList.Count ? oldList[c] : null;
                var newValue = c < newList.Count ? newList[c] : null;
                yield return Diff(oldValue, newValue, options, $"{c}");
            }
        }
    }

    private static DiffResult CompareDicts(Type type, IDictionary oldDictionary, IDictionary newDictionary, SimpleDiffOptions options, string path)
    {
        // if ( type.GenericTypeArguments?.Length!=2 ) throw new Exception("Unexpected dictionary type");
        // if (type.GenericTypeArguments[0] != typeof(string)) throw new NotImplementedException("Key is not string");

        var keys = allKeys().Distinct().ToArray();
        Array.Sort(keys);

        var delta = diff().Where(x => x != null).ToArray();
        if (delta.Length == 0) return null;

        return new ObjectDiffResult
        {
            OldValue = oldDictionary,
            NewValue = newDictionary,
            Path = path,
            Diffs = delta,
        };

        IEnumerable<DiffResult> diff()
        {
            foreach (var key in keys)
            {
                var oldValue = oldDictionary[key];
                var newValue = newDictionary[key];
                yield return Diff(oldValue, newValue, options, $"{key}");
            }
        }

        IEnumerable<object> allKeys()
        {
            foreach (var key in oldDictionary.Keys) yield return key;
            foreach (var key in newDictionary.Keys) yield return key;
        }
    }
}

public class SimpleDiffOptions
{
    public Dictionary<Type, Func<object, object, string, DiffResult>> Mapping { get; init; }

    /// <summary>
    /// Whether to skip properties marked with the JsonIgnore attribute
    /// </summary>
    public bool SkipJsonIgnore { get; init; }

    /// <summary>
    /// Whether to skip properties marked with the BsonIgnore attribute
    /// </summary>
    public bool SkipBsonIgnore { get; init; }

    /// <summary>
    /// Whether to skip properties marked with the SimpleDiffIgnore attribute
    /// </summary>
    public bool SkipSimpleDiffIgnore { get; init; } = true;
    
    /// <summary>
    /// when set, allow to exclude properties
    /// </summary>
    public Func<Type, PropertyInfo, bool> ExcludeProperty { get; init; }
    
    public HashSet<object> Visited { get; } = new HashSet<object>();
}

public class DiffResult
{
    private Type PropertyType { get; init; }
    public object OldValue { get; init; }
    public object NewValue { get; init; }
    public string Path { get; init; }

    public static DiffResult New<T>(T oldValue, T newValue, string path = null) => new()
    {
        NewValue = newValue,
        OldValue = oldValue,
        Path = path,
        PropertyType = oldValue?.GetType() ?? newValue?.GetType() ?? typeof(T),
    };
    
    public virtual void Traverse(Action<string[], string, DiffType, object, object> action, string[] parent)
    {
        action(parent, Path, OldValue==null ? DiffType.Set : NewValue==null ? DiffType.Unset : DiffType.Update, OldValue, NewValue);
    }
}

public class ObjectDiffResult : DiffResult
{
    public DiffResult[] Diffs { get; init; }

    public override void Traverse(Action<string[], string, DiffType, object, object> action, string[] parent)
    {
        var level = Path != null ? parent.Append(Path).ToArray() : parent;
        foreach (var diff in Diffs) diff.Traverse(action, level);
    }
}

public class ArrayDiffResult : DiffResult
{
    public DiffResult[] Diffs { get; init; }

    public override void Traverse(Action<string[], string, DiffType, object, object> action, string[] parent)
    {
        var level = parent.Append(Path).ToArray();
        foreach (var diff in Diffs)
        {
            diff.Traverse(action, level);
        }
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SimpleDiffIgnoreAttribute : Attribute
{
}

public enum DiffType
{
    Set,
    Unset, 
    Update, 
}

public static class DiffTypeExtensions
{
    public static string ToChangeList(this DiffResult diff)
    {
        if (diff == null) return "No changes";
        
        var str = "";
        diff.Traverse((path, name, t,  o, n) =>
        {
            var full = string.Join(".", path.Append(name));

            str += t switch
            {
                DiffType.Set => $"{full} (INIT): {n}\n",
                DiffType.Unset => $"{full} (UNSET): {o}\n",
                _ => $"{full}: {o} -> {n}\n",
            };
            
        }, []);

        return str;
    }
}