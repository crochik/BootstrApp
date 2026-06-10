using System;
using System.Collections.Generic;

namespace PI.Shared.Models
{
    public class EntityOpenSlots
    {
        public Guid AccountId { get; set; }
        public Guid EntityId { get; set; }

        public Guid AppointmentTypeId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public AppointmentTypeAvailability Availability { get; set; }
        public IEnumerable<TimeSlot> Slots { get; set; }
        public IEnumerable<CalendarEvent> Events { get; set; }
        public TimeZoneInfo TimeZoneInfo { get; set; }
        public AppointmentType AppointmentType { get; set; }
    }
}