using System;
using Newtonsoft.Json.Linq;
using PI.Shared.Models;

namespace FlowActions
{
    public class ParseContext
    {
        public Guid EntityId { get; set; }
        public dynamic Options { get; set; }
        public Guid EventIdTrigger { get; set; }
        public Guid? CurrentStatusId { get; set; }
        public Guid ActionId { get; set; }
        public string Description { get; set; }
        public string IconName { get; set; }
        public string ObjectType { get; set; }
        public IContextWithActor EntityContext { get; set; }

        public ParseContext() { }
    }

    public static class ParseContextExtensions
    {
        public static IEventType ParseNextEvent(this ParseContext context, string defaultName = null)
        {
            var obj = context.Options;
            var name = (obj["nextEventName"] as JValue)?.Value as string;
            name ??= defaultName;

            if (string.IsNullOrEmpty(name)) return null;

            // var eventType = (obj["nextEventType"] as JValue)?.Value as string;
            // if (string.IsNullOrWhiteSpace(eventType)) eventType = null; // base event type ??

            var description = (obj["nextEventDescription"] as JValue)?.Value as string;

            return new EventType
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                EntityId = context.EntityId,
                ObjectType = context.ObjectType,
                Trigger = new Trigger()
            };
        }
    }
}