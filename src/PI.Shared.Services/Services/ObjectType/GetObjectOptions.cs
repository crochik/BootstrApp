using System.Collections.Generic;
using PI.Shared.Models;

namespace PI.Shared.Services;

public interface IGetObjectCache
{
    ObjectType GetFromCache(string name);
    void OnObjectTypeLoaded(ObjectType objectType);
}

public class GetObjectOptions : IGetObjectCache
{
    public IGetObjectCache Cache { get; init; }
    
    public string Namespace { get; init; }
    public bool IncludeSuperNamespaces { get; init; }
    
    /// <summary>
    /// Whether to load base object types, if any
    /// </summary>
    public bool LoadBaseObject { get; init; } = true;
    
    public bool UseFieldApiNames { get; init; } = false;

    public GetObjectOptions()
    {
    }

    public GetObjectOptions(string @namespace, bool includeSuperNamespaces = false)
    {
        Namespace = @namespace;
        IncludeSuperNamespaces = includeSuperNamespaces;
    }

    protected GetObjectOptions(GetObjectOptions options)
    {
        if (options != null)
        {
            Namespace = options.Namespace;
            IncludeSuperNamespaces = options.IncludeSuperNamespaces;
            LoadBaseObject = options.LoadBaseObject;
            UseFieldApiNames = options.UseFieldApiNames;
            Cache = options.Cache;
        }
    }

    public virtual ObjectType GetFromCache(string name) => Cache?.GetFromCache(name);
    public virtual void OnObjectTypeLoaded(ObjectType objectType) => Cache?.OnObjectTypeLoaded(objectType);
}

public class GetObjectCache : IGetObjectCache
{
    private Dictionary<string, ObjectType> _cache = null;
    

    public ObjectType GetFromCache(string name)
    {
        if (_cache == null) return null;
        return _cache.TryGetValue(name, out var objectType) ? objectType : null;
    }

    public void OnObjectTypeLoaded(ObjectType objectType)
    {
        if (objectType == null) return;
        _cache ??= new Dictionary<string, ObjectType>();
        _cache[objectType.FullName] = objectType;
    }
}