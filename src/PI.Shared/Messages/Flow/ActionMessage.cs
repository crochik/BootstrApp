using System;
using Crochik.Messaging;
using Newtonsoft.Json;

namespace Messages.Flow;

public interface IActionMessage : IMessageBody
{
    IActionOptions GetActionOptions();
    FlowEvent GetEvent();
}

public abstract class ActionMessage<T> : IActionMessage
    where T : IActionOptions
{
    public T Options { get; set; }

    protected ActionMessage(T options)
    {
        Options = options;
    }

    protected ActionMessage() { }

    public string GetEventDescription(Guid? eventId, string message = null) => Options.GetEventDescription(eventId, message);

    public IActionOptions GetActionOptions() => Options;
    public abstract FlowEvent GetEvent();
}

public class SimpleActionMessage<TOptions> : ActionMessage<TOptions>
    where TOptions : IActionOptions
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    public string SerializedEvent { get; set; }
    public string EventType { get; set; }

    private FlowEvent _event = null;
        
    [JsonIgnore]
    public FlowEvent Event => _event ??= DeserializeEvent();

    public SimpleActionMessage() { }

    public SimpleActionMessage(FlowEvent evt, IActionOptions options)
    {
        Options = (TOptions)options;
        EventType = evt.GetType().FullName;
        SerializedEvent = JsonConvert.SerializeObject(evt, Settings);
    }

    private FlowEvent DeserializeEvent()
    {
        var type = typeof(IActionMessage).Assembly.GetType(EventType, true);
        return JsonConvert.DeserializeObject(SerializedEvent, type, Settings) as FlowEvent;
    }

    public override FlowEvent GetEvent() => Event;
}

[Obsolete("move away from it")]
public abstract class LeadWithApptActionMessage<T> : ActionMessage<T>
    where T : IActionOptions
{
    public LeadWithAppointmentEvent Event { get; set; }

    protected LeadWithApptActionMessage(LeadWithAppointmentEvent evt, T options) :
        base(options)
    {
        Event = evt;
    }

    protected LeadWithApptActionMessage() { }

    public override FlowEvent GetEvent() => Event;
}