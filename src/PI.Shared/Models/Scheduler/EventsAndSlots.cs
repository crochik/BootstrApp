using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models
{
    [BsonCollection("scheduler.EventsAndSlots")]
    public class EventsAndSlots
    {
        [BsonId]
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Guid SessionId { get; set; }
        public DateTime CreatedOn { get; set; }

        public Dictionary<Guid, EntityOpenSlots> Entities { get; set; } = new Dictionary<Guid, EntityOpenSlots>();
        public List<TimeSlot> Slots { get; set; } = new List<TimeSlot>();
    }
}