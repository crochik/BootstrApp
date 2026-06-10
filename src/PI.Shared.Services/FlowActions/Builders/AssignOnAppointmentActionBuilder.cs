using System;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;

namespace FlowActions;

public class AssignOnAppointmentActionBuilder : AssignActionBuilder
{
    public override string Name => "Assign lead to user scheduled for appointment";
    public override Guid Id => ActionIds.AssignLeadOnAppointment;

    public AssignOnAppointmentActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    // public override async Task<Form> GetFormAsync(FlowActionContext context)
    // {
    //     var builder = FormBuilder.New(nameof(AssignOnAppointmentAction).ToCamelCase());
    //     AddNextEventFields(context, builder, EventIds.OnAssignedEntityIdChanged);
    //     return await builder.BuildAsync();
    // }

    public override string[] InputObjectTypes => new[]
    {
        nameof(PI.Shared.Models.Appointment)
    };

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        var simpleEvent = evt as LeadWithAppointmentEvent;

        if (simpleEvent?.Appointment?.Appointment.EntityId == null)
        {
            return null;
        }

        return new AssignAction.Message
        {
            Options = new AssignActionOptions
            {
                NextEventId = (options as SimpleActionOptions).NextEventId,
                EntityId = simpleEvent.Appointment.Appointment.EntityId
            },
            Event = simpleEvent,
        };
    }
}