using System;
using System.Threading.Tasks;

namespace Crochik.Messaging
{
    public interface IMessageQueue {
        string Name {get;}
        
        void StartSubscription(Func<IMessage, Task> onMessageReceived, TypeMapper messageTypeMapper);
        void StopSubscription();
    }
}