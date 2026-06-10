using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace Messages.Flow;

public class GenericFlowEvent : FlowEvent
{
    public List<KeyValuePair<string, object>> RefValues { get; set; }
    public Dictionary<string, object> MetaValues { get; set; }

    [JsonIgnore] public override IEnumerable<KeyValuePair<string, object>> Refs => RefValues ?? Enumerable.Empty<KeyValuePair<string, object>>();

    [JsonIgnore] public override IEnumerable<KeyValuePair<string, object>> Meta => MetaValues ?? Enumerable.Empty<KeyValuePair<string, object>>();

    public GenericFlowEvent()
    {
    }

    public GenericFlowEvent(IFlowObject obj)
    {
        ObjectType = obj.ObjectType;
        TargetId = obj.Id;
        AccountId = obj.AccountId;
        StatusId = obj.ObjectStatusId;
        FlowId = obj.FlowId.GetValueOrDefault();
    }

    public GenericFlowEvent(FlowEvent evt)
    {
        ObjectType = evt.ObjectType;
        TargetId = evt.TargetId;
        AccountId = evt.AccountId;
        StatusId = evt.StatusId;
        FlowId = evt.FlowId;
        RunId = evt.RunId;
        
        // probably a bad idea since there is very little reason to duplicate an event "Exactly"
        EventTypeId = evt.EventTypeId;

        RefValues = evt.Refs?.ToList() ?? new List<KeyValuePair<string, object>>();
        MetaValues = evt.Meta != null ? new Dictionary<string, object>(evt.Meta) : new Dictionary<string, object>();
    }

    public void AddRefValue<T>(T model) where T : IFlowObject
    {
        if (model == null) return;
        AddRefValue(model.ObjectType, model.Id);
    }

    public void AddRefValue(string objectType, object id)
    {
        if (id == null) return;
        RefValues ??= new List<KeyValuePair<string, object>>();
        RefValues.Add(new KeyValuePair<string, object>($"{objectType}Id", id));
    }

    public void SetRefValue(string objectType, object id)
    {
        if (id == null) return;

        var key = $"{objectType}Id";

        RefValues = (RefValues ?? Enumerable.Empty<KeyValuePair<string, object>>())
            .Where(x => x.Key != key)
            .Append(new KeyValuePair<string, object>(key, id))
            .ToList();
    }

    public void SetMetaValue(string key, object value)
    {
        if (value == null || string.IsNullOrEmpty(key)) return;
        MetaValues ??= new Dictionary<string, object>();
        MetaValues[key] = value;
    }

    public bool TryAddMetaValue(string key, object value)
    {
        if (value == null || string.IsNullOrEmpty(key)) return false;
        MetaValues ??= new Dictionary<string, object>();
        return MetaValues.TryAdd(key, value);
    }

    public void AddRefValues(IEnumerable<KeyValuePair<string, object>> refs)
    {
        if (refs == null) return;

        // TODO: add distinct?
        // ...
        RefValues = (RefValues ?? Enumerable.Empty<KeyValuePair<string, object>>())
            .Concat(refs)
            .ToList();
    }
}

public class ObjectUpdatedEvent : GenericFlowEvent
{
    public string[] ModifiedFields { get; set; }
    public Dictionary<string, object> UpdatedValues { get; set; }
}