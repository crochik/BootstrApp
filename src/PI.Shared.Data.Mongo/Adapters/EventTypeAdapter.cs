using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class EventTypeAdapter : MappedNewModelAdapter<IEventType, EventType>, IEventTypeAdapter
    {
        public EventTypeAdapter(MongoConnection connection) : base(connection)
        {
        }
    }
}