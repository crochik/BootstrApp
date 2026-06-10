using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace Models
{
    public class SingerImportConfig
    {
        [BsonId]
        public Guid Id { get; set; }
        
        public string Name { get; set; }
        public Guid AccountId { get; set; }
        public Guid EntityId { get; set; }
        public Dictionary<string, SingerStreamConfig> Streams { get; set; } = new Dictionary<string, SingerStreamConfig>();
        public SingerState State { get; set; }
        public TapConfig TapConfig { get; set; }
        public string CurrentTag { get; set; }
        public DateTime? ExtractStartedOn { get; set; }
        public DateTime? LoadEndedOn { get; set; }
    }
}