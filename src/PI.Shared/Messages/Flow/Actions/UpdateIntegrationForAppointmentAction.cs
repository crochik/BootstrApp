using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    [Obsolete("the integration exporting the data should update the object directly")]
    public class UpdateIntegrationForAppointmentActionOptions : SimpleActionOptions
    {
    }

    [Obsolete("the integration exporting the data should update the object directly")]
    public class UpdateIntegrationForAppointmentAction : FlowAction<UpdateIntegrationForAppointmentActionOptions, UpdateIntegrationForAppointmentAction.Message>
    {
        public override Guid Id => ActionIds.UpdateIntegrationForAppointment;

        public class Message : LeadWithApptActionMessage<UpdateIntegrationForAppointmentActionOptions> { }
    }
}