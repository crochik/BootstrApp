using System;
using MongoDB.Bson.Serialization.Attributes;

namespace Models
{
    public class SingerJob
    {
        [BsonId]
        public Guid Id { get; set; }
        
        public Guid AccountId { get; set; }
        public Guid ConfigId { get; set; }
        public string Tag { get; set; }
        public string[] ExtractLog { get; set; }
        public SingerMetricMessage[] ExtractMetrics { get; set; }
        public DateTime StartedOn { get; set; } = DateTime.UtcNow;
        public DateTime? ExtractEndedOn { get; set; }
        public DateTime? LoadStartedOn { get; set; }
        public DateTime? LoadEndedOn { get; set; }
        public SingerState InitialState { get; set; }
        public SingerState State { get; set; }
    }
}