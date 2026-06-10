using System;

namespace Controllers.Models;

public class TimeZone
{
    public string DisplayName { get; set; }
    public string Id { get; set; }
}

public class UserWithSchedulingSettings
{
    public Guid UserId { get; set; }
    public Guid AppointmentTypeId { get; set; }
    public string Name { get; set; }
}