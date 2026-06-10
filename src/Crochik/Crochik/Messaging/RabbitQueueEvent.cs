using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Crochik.Messaging;

public class RabbitQueueEvent(BasicDeliverEventArgs args) : IMessage
{
    public bool WasAcknowledged { get; set; }
    public object Body { get; set; }
    public ulong Tag => Args.DeliveryTag;
    public IModel Channel { get; set; }
    public string RoutingKey => Args.RoutingKey;
    public BasicDeliverEventArgs Args { get; } = args;
    public string BodyType { get => Args?.BasicProperties.Type ?? Body?.GetType().Name; }

    public void Acknowledge()
    {
        if (WasAcknowledged) return;
        WasAcknowledged = true;
        Channel.BasicAck(Tag, false);
    }

    public void Reject(bool requeue = false)
    {
        if (WasAcknowledged) return;
        WasAcknowledged = true;
        Channel.BasicReject(Tag, requeue);
    }
}