using System;

namespace Controllers.Models
{
    public class CalendarSyncSettings {
        public string Id { get; set; }
        public Guid? IdentityId { get; set; }
        public string Name { get; set; }
        public Guid? TenantId { get; set; }
    }
}