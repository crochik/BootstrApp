using System.Collections.Generic;
using Newtonsoft.Json;

namespace Models
{
    //{"type": "timer", "metric": "http_request_duration", "value": 0.5118350982666016, "tags": {"endpoint": "limits", "status": "succeeded"}}
    public class SingerMetricMessage
    {
        [JsonProperty("type")]
        public SingerMetricType MetricType { get; set; }

        [JsonProperty("metric")]
        public SingerMetric Metric { get; set; }

        [JsonProperty("value")]
        public double Value { get; set; }

        // Constants for commonly used tags
        // endpoint
        // job_type
        // http_status_code
        // status
        [JsonProperty("tags")]
        public Dictionary<string, string> Tags { get; set; }
    }
}