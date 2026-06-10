using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class LoadAppointmentActionOptions : SimpleActionOptions
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Criterion
        {
            Next,
            NextActive,
            NextCanceled,
            Previous,
            PreviousActive,
            PreviousCanceled,
            FirstScheduled,
            LastScheduled
        }

        public Criterion Criteria { get; set; }
    }

    public class LoadAppointmentAction : FlowAction<LoadAppointmentActionOptions, LoadAppointmentAction.Message>
    {
        public class Message : LeadWithApptActionMessage<LoadAppointmentActionOptions> { }

        public override Guid Id => ActionIds.LoadAppointment;
    }
}