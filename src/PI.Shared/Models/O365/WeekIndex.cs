using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models
{
    //
    // Summary:
    //     The enum WeekIndex.
    [JsonConverter(typeof(StringEnumConverter))]
    public enum WeekIndex
    {
        //
        // Summary:
        //     first
        First = 0,
        //
        // Summary:
        //     second
        Second = 1,
        //
        // Summary:
        //     third
        Third = 2,
        //
        // Summary:
        //     fourth
        Fourth = 3,
        //
        // Summary:
        //     last
        Last = 4
    }
}