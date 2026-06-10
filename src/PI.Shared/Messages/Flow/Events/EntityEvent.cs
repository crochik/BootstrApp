using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace Messages.Flow
{
    public class EntityEvent : AbstractEntityEvent
    {
        public KeyValuePair<string, object>[] RefValues { get; set; }
        public Dictionary<string, object> MetaValues { get; set; }
        public override Guid TargetId { get; set; }
        public override Guid AccountId { get; set; }
        public override string ObjectType { get; set; }

        [JsonIgnore]
        public override IEnumerable<KeyValuePair<string, object>> Refs => RefValues ?? Enumerable.Empty<KeyValuePair<string, object>>();

        [JsonIgnore]
        public override IEnumerable<KeyValuePair<string, object>> Meta => MetaValues ?? Enumerable.Empty<KeyValuePair<string, object>>();

        public EntityEvent() { }

        public EntityEvent(IEntity entity)
        {
            TargetId = entity.Id;
            AccountId = entity.AccountId;
            ObjectType = entity.ObjectType;
            Entity = entity.Name;
            StatusId = entity.ObjectStatusId;
            FlowId = entity.FlowId.Value;
        }
    }
}