using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Models;

public class AppointmentIntegration : IIntegrationAppointment
{
    public Guid IntegrationId { get; set; }
    public string ExternalId { get; set; }
    public string Status { get; set; }
    public string Url { get; set; }

    [JsonIgnore]
    [BsonIgnore]
    public object Data { get; set; }

    [JsonIgnore]
    [BsonIgnore]
    public Guid AppointmentId { get; set; }

    [JsonIgnore]
    [BsonElement("Data")]
    public BsonDocument SerializedData
    {
        get => Data == null ? null : (Data is BsonDocument bson) ? bson : BsonDocument.Parse(JsonConvert.SerializeObject(Data));
        set => Data = value == null ? null : BsonSerializer.Deserialize<BsonDocument>(value);
    }
}