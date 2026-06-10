using MongoDB.Bson.Serialization.Attributes;

namespace Models
{
    [BsonIgnoreExtraElements]
    public class SingerJobSummary
    {
        [BsonId]
        public string Stream { get; set; }
        
        public int Added { get; set; }
        public int Exception { get; set; }
        public int Failed { get; set; }
        public int Merged { get; set; }
        public int Skip { get; set; }
        public int Unknown { get; set; }
        public int Updated { get; set; }
    }
}