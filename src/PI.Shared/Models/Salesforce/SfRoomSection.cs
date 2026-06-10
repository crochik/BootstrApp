using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;

public class SfRoomSection : SfObject
{
    [BsonElement("Name")] public string Name { get; set; }
    [BsonElement("Section__c")] public string SectionId { get; set; }
    [BsonElement("Room__c")] public string RoomId { get; set; }
}