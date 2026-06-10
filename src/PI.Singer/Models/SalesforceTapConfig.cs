using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Models
{
    [BsonDiscriminator(Required=true)]
    [BsonKnownTypes(typeof(SalesforceTapConfig))]
    public class TapConfig
    {
    }

    public class SingerDateConverter : IsoDateTimeConverter
    {
        public SingerDateConverter()
        {
            base.DateTimeFormat = "yyyy-MM-ddT00:00:00Z";
        }
    }

    public class SalesforceTapConfig : TapConfig
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum ApiType
        {
            BULK
        }

        [BsonIgnore]
        [JsonProperty("client_id")]
        public string ClientId { get; set; }

        [BsonIgnore]
        [JsonProperty("client_secret")]
        public string ClientSecret { get; set; }

        [BsonIgnore]
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("start_date")]
        [JsonConverter(typeof(SingerDateConverter))]
        public DateTime StartDate { get; set; }

        [JsonProperty("api_type")]
        public ApiType Type { get; set; } = ApiType.BULK;

        [JsonProperty("select_fields_by_default")]
        public bool SelectFieldsByDefault { get; set; }

        [JsonProperty("is_sandbox")]
        public bool IsSandbox { get; set; }
    }
}