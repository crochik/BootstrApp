using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models
{
    //
    // Summary:
    //     The enum RecurrenceRangeType.
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RecurrenceRangeType
    {
        //
        // Summary:
        //     end Date
        EndDate = 0,
        //
        // Summary:
        //     no End
        NoEnd = 1,
        //
        // Summary:
        //     numbered
        Numbered = 2
    }
}