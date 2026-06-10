using System;

namespace PI.Shared.Models
{
    public class AppointmentSearchResults : SearchResults<AppointmentSearchResult>
    {
    }

    public class AppointmentSearchResult
    {
        public Guid Id { get; set; }
        public Guid EntityId { get; set; }
        // public Guid AccountId { get; set; }
        // public Guid[] EntityIds { get; set; }
        public Guid CreatedById { get; set; }
        public Guid LeadId { get; set; }
        public string Entity { get; set; }
        public string Lead { get; set; }
        public string CreatedBy { get; set; }
        public string Tool { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string LocalDate { get; set; }
        public string LocalTime { get; set; }
    }
}
