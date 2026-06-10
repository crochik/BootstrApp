using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PI.Shared.Models;

public interface IExternalId
{
    string ExternalId { get; }
}

public class CustomObject : DynamicFlowObjectModel, ICustomProperties, IExternalId, ITaggable
{
    public static Dictionary<string, PropertyInfo> PropertyNames = typeof(CustomObject).GetProperties().ToDictionary(x => x.Name);

    public object this[string key]
    {
        get => PropertyNames.TryGetValue(key, out var prop) ? prop.GetValue(this) : (this.Properties != null && this.Properties.TryGetValue(key, out var value) ? value : null);
    }

    public Dictionary<string, object> Properties { get; set; }

    /// <summary>
    /// External Id for object
    /// For customobject collection, it must be unique for AccountId/EntityId/ObjectType (enforced by unique index)
    /// When the ObjectType.UniqueExternalId is set, it will be unique for AccountId/EntityId/ObjectType
    /// </summary>
    public string ExternalId { get; set; }

    public string[] Tags { get; set; }
}