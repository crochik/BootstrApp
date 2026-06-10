using System;
using System.Collections.Generic;

namespace Controllers.Models;

public class SlotsResp
{
    public IEnumerable<PI.Shared.Models.NamedSlot> SuggestedSlots { get; set; }
    public IEnumerable<PI.Shared.Models.TimeSlot> Slots { get; set; }
    public string TimeZoneId { get; set; }
    
    /// <summary>
    /// For AppointmentField, include list of users in the org 
    /// </summary>
    public UserWithSchedulingSettings[] Users { get; set; }
    
    /// <summary>
    /// If there is an appointment, the entity id
    /// If not, the Lead.AssignedEntityId
    /// </summary>
    public Guid? AssignedEntityId { get; set; }

    public SlotsResp()
    {
    }
}