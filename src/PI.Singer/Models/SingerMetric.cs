using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SingerMetric
    {
        [EnumMember(Value = "http_request_duration")]
        HttpRequestDuration,

        [EnumMember(Value = "record_count")]
        RecordCount,

        [EnumMember(Value = "job_duration")]
        JobDuration
    }
}