using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models
{
    // copy from Microsoft.Graph
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CalendarEventType
    {
        //
        // Summary:
        //     single Instance
        SingleInstance = 0,
        //
        // Summary:
        //     occurrence
        Occurrence = 1,
        //
        // Summary:
        //     exception
        Exception = 2,
        //
        // Summary:
        //     series Master
        SeriesMaster = 3
    }  
}