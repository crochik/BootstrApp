using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

public interface IModel<T> : IRow<T>
{
    Guid AccountId { get; }
    string Name { get; }
}

public interface IModel : IModel<Guid> { }

public interface IEntityOwnedModel : IModel
{
    Guid EntityId { get; }
    string Description { get; set; }
}

public enum FlowObjectEventRoute
{
    Any,
    Create,
    Update,
    Delete,
};

public class ObjectStatusMilestones
{
    /// <summary>
    /// Last time (value) it entered each object status (key) 
    /// </summary>
    public Dictionary<Guid, DateTime> Transitions { get; set; }
        
    /// <summary>
    /// List of delayed/user triggered events processed (to avoid processing the same again)
    /// </summary>
    public Dictionary<Guid, DateTime> TriggeredEvents { get; set; }
}
    
public interface IFlowObject : IModel
{
    string Description { get; }
    string ObjectType { get; }
    Guid? ObjectStatusId { get; }
    Guid? FlowId { get; }
    bool IsActive { get; }

    DateTime CreatedOn { get; }
    DateTime? LastModifiedOn { get; }
    Actor LastActor { get; }
        
    // ObjectStatusMilestones ObjectStatusMilestones { get; set; }
}

public interface ICustomProperties : IFlowObject // , IIndexedProperties 
{
    Dictionary<string, object> Properties { get; }
}

public static class CustomPropertiesExtensions
{
    /// <summary>
    /// Try to get the property
    /// implicitly try to convert the value to the type T
    /// will throw exception if the value can't be converted
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns> 
    public static bool TryGetProperty<T>(this ICustomProperties obj, string name, out T value)
    {
        if (obj.Properties.TryGetValue(name, out var propValue))
        {
            if (propValue is T rightType)
            {
                value = rightType;
                return true;
            }

            value = PropertyValueConverter.ConvertTo<T>(propValue);
            return true;
        }

        value = default;
        return false;
    }

    public static T GetValueOrDefault<T>(this ICustomProperties obj, string name, T defaultValue = default)
        => TryGetProperty<T>(obj, name, out T exists) ? exists : defaultValue;
}
    
public static class FlowObjectEventRouteExtensions
{
    public static string GetRoute(this FlowObjectEventRoute action, IFlowObject obj)
        => action.GetRoute(obj.ObjectType, obj.Id.ToString("N"));

    public static string GetRoute(this FlowObjectEventRoute action, ObjectType objectType, Guid id)
        => action.GetRoute(objectType.Name, id.ToString("N"));

    public static string GetRoute(this FlowObjectEventRoute action, string objectType, Guid id)
        => action.GetRoute(objectType, id.ToString("N"));

    public static string GetRoute(this FlowObjectEventRoute action, string objectType, string id)
        => action switch
        {
            FlowObjectEventRoute.Any when id == null => $"object.{objectType}.#",
            FlowObjectEventRoute.Any => $"object.{objectType}.{id}.*",
            _ => $"object.{objectType}.{id ?? "*"}.{action.ToString().ToLower()}"
        };
}
    
public class FlowObjectModel : EntityOwnedModel, IFlowObject
{
    [BsonIgnore]
    public virtual string ObjectType => GetType().Name;

    public Guid? ObjectStatusId { get; set; }

    public Guid? FlowId { get; set; }

    public bool IsActive { get; set; } = true;
        
    // public ObjectStatusMilestones ObjectStatusMilestones { get; set; }
}

public class DynamicFlowObjectModel : EntityOwnedModel, IFlowObject
{
    public string ObjectType { get; set; }

    public Guid ObjectTypeId { get; set; }

    public Guid? ObjectStatusId { get; set; }

    public Guid? FlowId { get; set; }

    public bool IsActive { get; set; } = true;
        
    // public ObjectStatusMilestones ObjectStatusMilestones { get; set; }
}
    
public class Model<T> : IModel<T>
{
    [BsonId]
    public T Id { get; set; }
    public Guid AccountId { get; set; }
    public string Name { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedOn { get; set; }
    public Actor LastActor { get; set; }
}

public class IdOnlyModel
{
    [BsonId]
    [BsonSerializer(typeof(MagicGuidSerializer))]
    public Guid Id { get; set; }
}

public class Model : IModel<Guid>
{
    public const string IdFieldName = "_id";

    // fields and properties
    // [BsonExtraElements]
    // public BsonDocument BsonExtraElements { get; set; }

    [BsonId]
    [BsonSerializer(typeof(MagicGuidSerializer))]
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Name { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedOn { get; set; }
    public Actor LastActor { get; set; }


    public static Guid NewGuid() => Guid.NewGuid();
    public static Guid NewObjectId() => ObjectId.GenerateNewId().ToGuid();

    public static bool TryParseGuid(string objectId, out Guid id)
    {
        if (objectId.Length == 24)
        {
            // ObjectId
            id = ObjectId.Parse(objectId).ToGuid();
            return true;
        }

        // regular uuid
        return Guid.TryParse(objectId, out id);
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public class UseObjectIdAttribute : Attribute
{
    public UseObjectIdAttribute()
    {
    }
}

