using System.Threading.Tasks;

namespace Crochik.Messaging;

public interface IMessageBody
{
}

public interface IMessageBroker
{
    void Bind(IMessageQueue queue, string routingKey, string exchangeName = null);
    
    Task PublishAsync(string topic, string body, string exchangeName = null, string contentType = null);
    
    Task PublishAsync(string topic, IMessageBody body, string exchangeName = null);
    IMessageQueue CreateSubscription(QueueConfig queueConfig);
}