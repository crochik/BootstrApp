using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class ExportAppointmentToOffice365ActionOptions : SimpleActionOptions
    {
    }

    public class ExportAppointmentToOffice365Action : FlowAction<ExportAppointmentToOffice365ActionOptions, ExportAppointmentToOffice365Action.Message>
    {
        public override Guid Id => ActionIds.ExportAppointmentToOffice365;
        public class Message : LeadWithApptActionMessage<ExportAppointmentToOffice365ActionOptions> { }
    }
}