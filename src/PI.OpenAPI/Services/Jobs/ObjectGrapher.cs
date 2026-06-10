using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace PI.OpenAPI.Services.Jobs;

public class ObjectGrapher
{
    public Dictionary<string, ObjectType> ObjectTypes { get; init; }
    public HashSet<string> ObjectTypeDeps { get; } = new();
    public HashSet<Guid> FlowIds { get; } = new();
    public HashSet<Guid> ObjectStatusIds { get; } = new();
    public HashSet<Guid> ProfileIds { get; } = new();
    public HashSet<Guid> EventTypeIds { get; } = new();

    public static ObjectGrapher Graph(List<ObjectType> objectTypes)
    {
        return new ObjectGrapher(objectTypes);
    }

    private ObjectGrapher(List<ObjectType> objectTypes)
    {
        ObjectTypes = objectTypes.ToDictionary(x => x.FullName);
    }

    public void Graph()
    {
        foreach (var objectType in ObjectTypes.Values)
        {
            if (objectType.BaseObjectType != null) ObjectTypeDeps.Add(objectType.BaseObjectType);
            if (objectType.FlowId.HasValue) FlowIds.Add(objectType.FlowId.Value);
            if (objectType.InitialFlowId.HasValue) FlowIds.Add(objectType.InitialFlowId.Value);
            if (objectType.ObjectStatusId.HasValue) ObjectStatusIds.Add(objectType.ObjectStatusId.Value);
            if (objectType.InitialObjectStatusId.HasValue) ObjectStatusIds.Add(objectType.InitialObjectStatusId.Value);

            AddProfiles(objectType);
            ProcessFields(objectType.Fields);
        }
    }

    private void ProcessFields(Dictionary<string, FieldTemplate> fields)
    {
        if (fields == null) return;
        foreach (var field in fields)
        {
            if (field.Value.Field?.Options is ReferenceFieldOptions options)
            {
                Process(field.Value, options);
            }
        }
    }

    private void Process(FieldTemplate field, ReferenceFieldOptions options)
    {
        if (options.ObjectType != null)
        {
            ObjectTypeDeps.Add(options.ObjectType);
        }
        
        tryAdd(field.InitialValue);
        tryAdd(field.CalculatedValue);
        tryAdd(field.Field.DefaultValue);
        return;

        void tryAdd(object value)
        {
            if (value.TryToParseObjectId(out var id))
            {
                switch (options.ObjectType)
                {
                    case "Flow":
                        FlowIds.Add(id);
                        break;
                    case "ObjectStatus":
                        ObjectStatusIds.Add(id);
                        break;
                }
            }
        }
    }
    
    private void AddProfiles(ObjectType objectType)
    {
        if (objectType.RBAC?.Permissions == null) return;
        foreach (var permission in objectType.RBAC.Permissions)
        {
            if (Guid.TryParse(permission.Key, out var profileId))
            {
                ProfileIds.Add(profileId);
            }
        }
    }

    public void Graph(List<Flow> flows)
    {
        foreach (var flow in flows)
        {
            if (flow.Steps != null)
            {
                foreach (var step in flow.Steps)
                {
                    EventTypeIds.Add(step.EventIdTrigger);
                    if (step.CurrentStatusId.HasValue) ObjectStatusIds.Add(step.CurrentStatusId.Value);
                }
            }
        }
    }
}