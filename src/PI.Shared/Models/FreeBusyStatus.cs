using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models
{
    // copy from Microsoft.Graph
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FreeBusyStatus
    {
        Unknown = -1,
        Free = 0,
        Tentative = 1,
        Busy = 2,
        Oof = 3,
        WorkingElsewhere = 4
    }

    // copy from Microsoft.Graph
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Sensitivity
    {
        //
        // Summary:
        //     Normal
        Normal = 0,
        //
        // Summary:
        //     Personal
        Personal = 1,
        //
        // Summary:
        //     Private
        Private = 2,
        //
        // Summary:
        //     Confidential
        Confidential = 3
    }    

    // copy from Microsoft.Graph
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ResponseType
    {
        //
        // Summary:
        //     None
        None = 0,
        //
        // Summary:
        //     Organizer
        Organizer = 1,
        //
        // Summary:
        //     Tentatively Accepted
        TentativelyAccepted = 2,
        //
        // Summary:
        //     Accepted
        Accepted = 3,
        //
        // Summary:
        //     Declined
        Declined = 4,
        //
        // Summary:
        //     Not Responded
        NotResponded = 5
    }    
}