namespace PI.Shared.Models
{
    // public class RawAvailability
    // {
    //     public Guid UserId { get; set; }
    //     public System.DayOfWeek DayId { get; set; }
    //     public int StartMinutes { get; set; }
    //     public int DurationMinutes { get; set; }
    //     public string[] AppontmentTypeIds { get; set; }
    // }

    public class PostalCodeLookup
        {
            public string Code { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Country { get; set; }
        }
}