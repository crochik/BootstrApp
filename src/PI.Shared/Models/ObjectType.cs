using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Models;

public enum ActionEndpoint
{
    Get,
    Create,
    Update,
    Delete,
    Filter,
    // hack for the generation only?
    Recent,
    DataView, 
}

public enum IndexType
{
    Generic, 
    Autocomplete, 
    Search,
}

public class Index
{
    /// <summary>
    /// Index name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Fields to be used in auto complete
    /// </summary>
    public string[] AutoCompleteFieldNames { get; set; }
    
    /// <summary>
    /// Fields to search by word
    /// </summary>
    public string[] SearchFieldNames { get; set; }
    
    /// <summary>
    /// Fields that can be used to filter
    /// </summary>
    public string[] FilterFieldNames { get; set; }
}

public class Indices
{
    public Index SearchIndex { get; set; }
}

// TODO: could make it a profileElement and then it could
// - permissions per profile
// - independent configuration per profile
// ...
[BsonCollection("ObjectType.1")]
public class ObjectType : FlowObjectModel, ITaggable
{
    public const string ObjectTypeFullName = "ObjectType";

    /// <summary>
    /// Initial Flow Id (to be used by code that is not using the object type service to create objects)
    /// should make sure to also set the default value for the appropriate field
    /// </summary>
    public Guid? InitialFlowId { get; set; }

    /// <summary>
    /// Initial Object Status Id (to be used by code that is not using the object type service to create objects)
    /// should make sure to also set the default value for the appropriate field
    /// </summary>
    public Guid? InitialObjectStatusId { get; set; }

    /// <summary>
    /// New way to enforce uniqueness 
    /// </summary>
    public UniqueIndex[] UniqueIndices { get; set; }

    /// <summary>
    /// Control permissions to change the object
    /// </summary>
    public ObjectTypeRBAC RBAC { get; set; }

    /// <summary>
    /// All object fields
    /// </summary>
    public Dictionary<string, FieldTemplate> Fields { get; set; }

    /// <summary>
    /// Collection Name where this object is stored
    /// </summary>
    public string CollectionName { get; set; }

    /// <summary>
    /// When set, allows collection to leave in different database within the same server
    /// </summary>
    public string DatabaseName { get; set; }

    /// <summary>
    /// Discriminator for sub Types
    /// </summary>
    public Dictionary<string, Criteria> Discriminator { get; set; }

    /// <summary>
    /// Constraints / Initial values for each role/profile (aggregate from all matches)
    /// for derived types probably is a copy of the discriminator in the parent 
    /// </summary>
    public Dictionary<string, Criteria> Constraints { get; set; }

    public RelatedObjectType[] RelatedObjectTypes { get; set; }

    /// <summary>
    /// When this object type is derived from another one
    /// </summary>
    public string BaseObjectType { get; set; }

    /// temporary until it becomes a first class element
    public string[] Tags { get; set; }

    /// <summary>
    /// Configuration of how to build lookups for this object
    /// </summary>
    public LookupFields LookupFields { get; set; }

    /// <summary>
    /// Optional namespace 
    /// </summary>
    public string Namespace { get; set; }
    
    /// <summary>
    /// API name used for this object
    /// </summary>
    public string ApiName { get; set; }
    
    /// <summary>
    /// Label to be used to identify Object
    /// </summary>
    public string Label { get; set; }
    
    /// <summary>
    /// Label to be used to identify Multiple Objects
    /// </summary>
    public string LabelPlural { get; set; }
    
    /// <summary>
    /// Override api path for the action
    /// Value should start and end with '/' (e.g. /api/v1/ )
    /// </summary>
    public Dictionary<string, string> ApiPaths { get; set; }

    /// <summary>
    /// Whether it is an embedded object (not a top level)
    /// </summary>
    public bool IsEmbedded { get; set; }

    /// <summary>
    /// Whether this object can be created or not (only makes sense for objects that have discriminator?)
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// whether this object supports full text search 
    /// </summary>
    public bool IsFullTextSearchable { get; set; } = true;
    
    /// <summary>
    /// Indices available for this object
    /// </summary>
    public Indices Indices { get; set; }

    // not loaded by default (each integration will define its own model)
    // public Dictionary<string, ObjectTypeIntegration> Integrations { get; set; }

    /// <summary>
    /// ExternalId value is unique for the entire account / objectType
    /// Trying to insert a record with the same externalId of an existing one in the account will replace it
    /// </summary>
    [Obsolete("move away to a more generic uniqueness")]
    public bool UniqueExternalId { get; set; }

    /// <summary>
    /// TODO: too narrow/vague, remove it 
    /// </summary>
    [Obsolete]
    public string NativeType { get; set; }

    /// <summary>
    /// Helper to identify Custom Object
    /// TODO: too narrow/vague, remove it
    /// </summary>
    [Obsolete]
    public bool IsCustom => NativeType == null;

    [Obsolete("not cool")]
    public Type GetNativeType() => NativeType == null ? typeof(CustomObject) : Type.GetType(NativeType);

    /// <summary>
    /// When resolving the object type "cache" the base object type
    /// </summary>
    [BsonIgnore]
    // [JsonIgnore]
    public ObjectType LoadedBaseObjectType { get; set; }

    [BsonIgnore] public Dictionary<string, FieldOverride> OverriddenFields { get; set; }

    [BsonElement] 
    public string FullName => GetFullName(Name, Namespace);

    public string SafeFullName => GetSafeFullName(FullName);
    
    /// <summary>
    /// Whether objects of this type JUST use the default flow (they won't have a flow field)
    /// TODO: should also check whether other fields are of the flow object
    /// ... 
    /// </summary>
    [BsonIgnore]
    public bool UsesDefaultFlow => InitialFlowId.HasValue && !Fields.ContainsKey(nameof(IFlowObject.FlowId));   

    /// <summary>
    /// Get readable properties as a dictionary (the key is the field name)
    /// NOTE: it will not enforce RBAC for complex fields
    /// </summary>
    public Dictionary<string, object> UnsafeFlatten(IEntityContext context, IDictionary<string, object> dynamicObject, bool skipComplex = false)
    {
        if (dynamicObject == null) return null;

        var result = new Dictionary<string, object>();
        foreach (var field in Fields.Values)
        {
            if (!field.RBAC.CanRead(context)) continue;
            if (field.Field is IComplexFieldValue && skipComplex) continue;
            if (!dynamicObject.TryGetFieldValue(field.Field.Name, out var value) || value == null) continue;

            result[field.Field.Name] = value;
        }

        return result;
    }

    public string GetFormName(FormName formName) => $"objectType://{FullName}/{formName}";
    public string GetDefaultDataViewName(string hash = null) => $"objectType://{FullName}{(string.IsNullOrWhiteSpace(hash) ? string.Empty : "#" + hash)}";

    public IEnumerable<Condition> GetEqConditions(IEntityContext context)
    {
        // last for each field value
        var result = new Dictionary<string, Condition>();
        var conditions = GetConditions(context);
        foreach (var condition in conditions)
        {
            if (condition.Operator != Operator.Eq) continue;
            result[condition.FieldName] = condition;
        }

        return result.Values;
    }

    public IEnumerable<Condition> GetConditions(IEntityContext context)
    {
        if (Constraints == null) yield break;

        var keys = getContextKeys();
        foreach (var key in keys)
        {
            if (!Constraints.TryGetValue(key, out var constraints) || constraints?.Conditions == null) continue;

            foreach (var condition in constraints.Conditions)
            {
                yield return condition;
            }
        }

        IEnumerable<string> getContextKeys()
        {
            switch (context.Role)
            {
                case EntityRoleId.Account:
                    yield return nameof(EntityRoleId.Account);
                    break;
                case EntityRoleId.Admin:
                    yield return nameof(EntityRoleId.Account);
                    yield return nameof(EntityRoleId.Admin);
                    break;
                case EntityRoleId.Organization:
                    yield return nameof(EntityRoleId.Account);
                    yield return nameof(EntityRoleId.Organization);
                    break;
                case EntityRoleId.Manager:
                    yield return nameof(EntityRoleId.Account);
                    yield return nameof(EntityRoleId.Organization);
                    yield return nameof(EntityRoleId.Manager);
                    break;
                case EntityRoleId.User:
                    yield return nameof(EntityRoleId.Account);
                    yield return nameof(EntityRoleId.Organization);
                    yield return nameof(EntityRoleId.User);
                    break;
                case EntityRoleId.Profile:
                    yield return nameof(EntityRoleId.Account);
                    break;
                default:
                    throw new ForbiddenException(context);
            }

            if (context.ProfileId.HasValue)
            {
                if (context.AllProfileIds.Length > 1)
                {
                    foreach (var profileId in context.AllProfileIds) yield return profileId.ToString();
                }
                else
                {
                    yield return context.ProfileId.ToString();    
                }
            }
        }
    }

    /// <summary>
    /// Enumerate any flowIds the object has defined (using fields)
    /// </summary>
    public IEnumerable<Guid> GetFlowIds(ExpandoObject rawObject)
    {
        var dict = (IDictionary<string, object>)rawObject;
     
        // hard coded
        // TODO: remove me
        if (dict.TryGetGuidParam(nameof(IFlowObject.FlowId), out var flowId))
        {
            yield return flowId;
        }

        if (Fields == null) yield break;

        var otherFields = Fields.Values
            .Select(x => x.Field)
            .OfType<ReferenceField>()
            .Where(x => x.ReferenceFieldOptions.ObjectType == nameof(Flow));

        foreach (var field in otherFields)
        {
            if (dict.TryResolvePathGuidValue(field.Name, out var otherFlowId) && otherFlowId != flowId)
            {
                yield return otherFlowId;
            }
        }

        var otherMultiFields = Fields.Values
            .Select(x => x.Field)
            .OfType<MultiReferenceField>()
            .Where(x => x.MultiReferenceFieldOptions.ObjectType == nameof(Flow));

        foreach (var field in otherMultiFields)
        {
            if (dict.TryResolvePathValue(field.Name, out var value))
            {
                switch (value)
                {
                    case IEnumerable<Guid> ids:
                        foreach (var id in ids)
                        {
                            yield return id;
                        }

                        break;
                    default:
                        Console.WriteLine("Unexpected value");
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Enumerate any flowIds the object has defined (using fields)
    /// </summary>
    public IEnumerable<Guid> GetFlowIds(Dictionary<string, object> dict)
    {
        // hard coded
        // TODO: remove me
        if (dict.TryGetGuidParam(nameof(IFlowObject.FlowId), out var flowId))
        {
            yield return flowId;
        }

        if (Fields == null) yield break;

        var otherFields = Fields.Values
            .Select(x => x.Field)
            .OfType<ReferenceField>()
            .Where(x => x.ReferenceFieldOptions.ObjectType == nameof(Flow));

        foreach (var field in otherFields)
        {
            if (dict.TryGetGuidParam(field.Name, out var otherFlowId) && otherFlowId != flowId)
            {
                yield return otherFlowId;
            }
        }

        var otherMultiFields = Fields.Values
            .Select(x => x.Field)
            .OfType<MultiReferenceField>()
            .Where(x => x.MultiReferenceFieldOptions.ObjectType == nameof(Flow));

        foreach (var field in otherMultiFields)
        {
            if (dict.TryGetValue(field.Name, out var value))
            {
                switch (value)
                {
                    case IEnumerable<Guid> ids:
                        foreach (var id in ids)
                        {
                            yield return id;
                        }

                        break;
                    default:
                        Console.WriteLine("Unexpected value");
                        break;
                }
            }
        }
    }
    
    /// <summary>
    /// Get the base objects (when they were loaded)
    /// </summary>
    /// <returns></returns>
    public IEnumerable<string> GetLoadedBaseObjectTypeNames()
    {
        if (string.IsNullOrEmpty(BaseObjectType)) yield break;
        if (LoadedBaseObjectType == null || LoadedBaseObjectType.FullName != BaseObjectType)
        {
            // ERROR: haven't loaded the base object
            // ... 
            yield break;
        }

        yield return BaseObjectType;
        foreach (var obj in LoadedBaseObjectType.GetLoadedBaseObjectTypeNames()) yield return obj;
    }

    /// <summary>
    /// Calculate full name
    /// If name contains namespace, ignore namespace parameter
    /// </summary>
    public static string GetFullName(string objectTypeName, string @namespace = null)
        => objectTypeName == null || objectTypeName.Contains('.') ?
            objectTypeName :
            string.IsNullOrEmpty(@namespace) ? objectTypeName : $"{@namespace}.{objectTypeName}";

    /// <summary>
    /// Extract namespace (if any) from full object Type name
    /// </summary>
    public static string GetNamespace(string fullObjectTypeName)
    {
        var index = fullObjectTypeName.LastIndexOf('.');
        return index > 0 ? fullObjectTypeName.Substring(0, index) : null;
    }
    
    /// <summary>
    /// Extract just the object name from fullname
    /// </summary>
    public static string GetName(string fullObjectTypeName)
    {
        var index = fullObjectTypeName.LastIndexOf('.');
        return index > 0 ? fullObjectTypeName.Substring(index + 1) : fullObjectTypeName;
    }
    
    public static string GetSafeFullName(string fullName) => fullName.Replace('.', '|');

    public bool TryGetObjectTypeFromFlowField(out string objectTypeName) => TryGetObjectTypeFromField(out objectTypeName, nameof(IFlowObject.FlowId));
    public bool TryGetObjectTypeFromObjectStatusField(out string objectTypeName) => TryGetObjectTypeFromField(out objectTypeName, nameof(IFlowObject.FlowId));

    private bool TryGetObjectTypeFromField(out string objectTypeName, string fieldName)
    {
        if (!Fields.TryGetValue(fieldName, out var ft) || ft.Field is not ReferenceField referenceField)
        {
            objectTypeName = null;
            return false;
        }
        
        return referenceField.TryGetObjectTypeFromCriteria(out objectTypeName, FullName);
    }
}