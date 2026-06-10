using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models
{
    public class AppIntegration : IModel
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string Description { get; set; }
        public string ServiceName { get; set; }
        public string Name { get; set; }
    }

    public interface IIntegration
    {
        Guid IntegrationId { get; }
        string Data { get; }
        string Authentication { get; }
    }

    public class LeadTypeIntegration : IIntegration
    {
        public Guid IntegrationId { get; set; }
        public string Data { get; set; }
        public string Authentication { get; set; }

        [BsonIgnore]
        public Guid LeadTypeId { get; set; }
    }

    public class AppointmentTypeIntegration : IIntegration
    {
        public Guid IntegrationId { get; set; }
        public string Data { get; set; }
        public string Authentication { get; set; }

        [BsonIgnore]
        public Guid AppointmentTypeId { get; set; }
    }


    [JsonConverter(typeof(StringEnumConverter))]
    public enum EntityTrunkLevel
    {
        User,
        Organization,
        Account
    };

    public interface IEntityTrunkIntegration : IIntegration
    {
        EntityTrunkLevel Level { get; }
    }

    public static class IntegrationExtensions
    {
        public static T GetAuthentication<T>(this IIntegration obj) where T : class
        {
            // TODO: encrypt
            return obj.Authentication != null ? JsonConvert.DeserializeObject<T>(obj.Authentication) : null;
        }

        public static T GetData<T>(this IIntegration obj) where T : class
        {
            return obj.Data != null ? JsonConvert.DeserializeObject<T>(obj.Data) : null;
        }
    }
}