using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.SingleUseTickets;

[BsonCollection("SingleUseTicket")]
[BsonDiscriminator(Required = true)]
public class SingleUseTicket : Model
{
    public Guid EntityId { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public bool IsActive { get; set; }
}

public class IntegrationSingleUseTicket : SingleUseTicket
{
    public Guid IntegrationId { get; set; }
}