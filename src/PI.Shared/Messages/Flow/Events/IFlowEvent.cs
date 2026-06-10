using System;
using System.Threading.Tasks;
using Crochik.Messaging;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace Messages.Flow;
// public interface FlowEvent : IMessageBody
// {
//     string Action { get; set; }
//     string Description { get; set; }
//     Actor Actor { get; set; }

//     Guid RunId { get; }

//     // IFlowObject?
//     Guid TargetId { get; }
//     Guid FlowId { get; }
//     Guid? StatusId { get; }
//     Guid AccountId { get; }
//     string ObjectType { get; }

//     IEnumerable<KeyValuePair<string, object>> Refs { get; }
//     IEnumerable<KeyValuePair<string, object>> Meta { get; }
// }

public static class IMessageBrokerExtensions
{
    /// <summary>
    /// Only used by stripservice, have to just be careful to replace it there  
    /// </summary>
    [Obsolete("event should include the eventtypeid")]
    public static async Task DispatchAsync<T>(this IMessageBroker broker, T flowEvent, Guid? eventId, bool error = false) where T : FlowEvent
    {
        if (eventId.HasValue)
        {
            await broker.DispatchAsync(flowEvent, eventId.Value, error);
        }
    }

    public static async Task DispatchAsync<T>(this IMessageBroker broker, T flowEvent, bool error = false) where T : FlowEvent
    {
        if (!flowEvent.EventTypeId.HasValue) return;
        await broker.DispatchAsync(flowEvent, EventIds.GetRoute(flowEvent.EventTypeId.Value, error));
    }

    private static Task DispatchAsync<T>(this IMessageBroker broker, T flowEvent, Guid eventId, bool error = false) where T : FlowEvent
        => broker.DispatchAsync(flowEvent, EventIds.GetRoute(eventId, error));

    public static Task DispatchAsync<T>(this IMessageBroker broker, T flowEvent, string route) where T : FlowEvent
        => broker.PublishAsync(route, flowEvent);
        
}