using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    [Obsolete]
    public class ExportAppointmentToInspireNetActionOptions : SimpleActionOptions
    {
        public override ActionOutput[] Output
        {
            get => new[]
            {
                new ActionOutput
                {
                    EventId = NextEventId,
                    Name = "Default",
                    Description = "Appointment exported"
                    // EventType = EventTypeCode.AppointmentIntegration
                }
            };
        }
    }

    [Obsolete]
    public class ExportAppointmentToInspireNetAction : FlowAction<ExportAppointmentToInspireNetActionOptions, ExportAppointmentToInspireNetAction.Message>
    {
        public override Guid Id => ActionIds.ExportAppointmentToInspireNet;
        public class Message : LeadWithApptActionMessage<ExportAppointmentToInspireNetActionOptions> { }
    }
}