using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Crochik.Messaging
{
    public class NoopMessageBroker : IMessageBroker
    {
        private static IMessageQueue messageQueue = new NoopMessageQueue();

        public string DefaultExchangeName => "NOOP";

        public NoopMessageBroker(ILogger<NoopMessageBroker> logger)
        {
            logger.LogWarning("RabbitMq is not configured, using NOOP");
        }

        public void Bind(IMessageQueue queue, string routingKey, string exchangeName = null)
        {
        }

        public IMessageQueue CreateSubscription(QueueConfig queueConfig)
        {
            return messageQueue;;
        }
        
        public void Publish(string topic, string body, string exchangeName = null, string contentType = null, string objectTypen = null)
        {
        }

        public void Publish(string topic, IMessageBody body, string exchangeName = null)
        {

        }

        public Task PublishAsync(string topic, string body, string exchangeName = null, string contentType = null)
        {
            return Task.CompletedTask;
        }

        public Task PublishAsync(string topic, IMessageBody body, string exchangeName = null)
        {
            return Task.CompletedTask;
        }
    }
}