using System;
using System.Threading.Tasks;

namespace Crochik.Messaging
{
    public class NoopMessageQueue : IMessageQueue
    {

        public string Name => "NOOP";
        public ISubscription Subscription { get; }= new NoopSubscription();
        
        public ISubscription Subscribe(Func<IMessage, Task> handler, bool autoAck = true)
        {
            return Subscription;
        }

        public void StartSubscription(Func<IMessage, Task> onMessageReceived, TypeMapper messageTypeMapper)
        {
        }

        public void StopSubscription()
        {
        }
    }
}