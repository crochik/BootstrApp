using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.GeoJSON;

public class Point
{
    [BsonElement("type")] public string Type => "Point";
    
    [BsonElement("coordinates")]
    public decimal[] Coordinates { get; set; }
}