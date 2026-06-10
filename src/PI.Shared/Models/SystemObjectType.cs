using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public static class SystemObjectType
    {
        public const string Unknown = nameof(SystemObjectType.Unknown);
        public const string Appointment = nameof(SystemObjectType.Appointment);
        public const string Lead = nameof(SystemObjectType.Lead);
        public const string User = nameof(SystemObjectType.User);
        public const string Organization = nameof(SystemObjectType.Organization);
        public const string Account = nameof(SystemObjectType.Account);
    }
}