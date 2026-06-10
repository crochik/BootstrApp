using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models.Expressions;

namespace Models
{
    [BsonDiscriminator(Required = true)]
    [BsonKnownTypes(
        typeof(AppointmentStreamConfig),
        typeof(LeadStreamConfig),
        typeof(OrganizationMembershipStreamConfig),
        typeof(OrganizationStreamConfig),
        typeof(UserStreamConfig)
    )]
    public abstract class SingerStreamConfig
    {
        public string Name { get; set; }
        public Condition[] InactiveConditions { get; set; }
    }
}