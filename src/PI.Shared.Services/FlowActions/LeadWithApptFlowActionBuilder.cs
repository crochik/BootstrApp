using Crochik.Mongo;
using Messages.Flow;

namespace FlowActions;

public abstract class LeadWithApptFlowActionBuilder<T, TMessage> :AbstractFlowActionBuilder<T, TMessage>
    where T : SimpleActionOptions, new()
    where TMessage : LeadWithApptActionMessage<T>, new()
{
    protected LeadWithApptFlowActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        var simpleEvent = evt as LeadWithAppointmentEvent;
        var simpleOptions = options as T;

        return simpleEvent == null || simpleOptions == null ? null : new TMessage
        {
            Event = simpleEvent,
            Options = simpleOptions
        };
    }
}