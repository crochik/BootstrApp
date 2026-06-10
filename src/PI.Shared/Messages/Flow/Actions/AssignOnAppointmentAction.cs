using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class AssignOnAppointmentAction : AssignAction
    {
        public override Guid Id => ActionIds.AssignLeadOnAppointment;
    }
}