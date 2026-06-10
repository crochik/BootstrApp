using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models
{
    //
    // Summary:
    //     The enum RecurrencePatternType.
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RecurrencePatternType
    {
        //
        // Summary:
        //     daily
        Daily = 0,
        //
        // Summary:
        //     weekly
        Weekly = 1,
        //
        // Summary:
        //     absolute Monthly
        AbsoluteMonthly = 2,
        //
        // Summary:
        //     relative Monthly
        RelativeMonthly = 3,
        //
        // Summary:
        //     absolute Yearly
        AbsoluteYearly = 4,
        //
        // Summary:
        //     relative Yearly
        RelativeYearly = 5
    }  
}