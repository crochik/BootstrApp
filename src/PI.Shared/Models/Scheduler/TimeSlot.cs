using System;

namespace PI.Shared.Models
{
    public class TimeSlot
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public class NamedSlot : TimeSlot
    {
        public string Name { get; set; }
    }
    
    public class TimeSlotWithCount : TimeSlot
    {
        public string Tag { get; set; }
        public int Count { get; set; }
    }

    public class XTimeSlot : TimeSlot 
    {
        public string Tag { get; set; }
        public string LocalDate { get; set; }
        public int WeekNumber { get; set; }
    }

    public class TimeBlockStats
    {
        public DateTime? FirstDate { get; set; }
        public int Count { get; set; }
        public string Name { get; set; }
    }
}