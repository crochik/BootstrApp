namespace Crochik.Messaging
{
    public class NoopSubscription : ISubscription
    {
        public string QueueName => "NOOP";

        public bool AutoAck => true;

        public bool Start(ITypeMapper typeMapper = null)
        {
            return true;
        }

        public bool Stop()
        {
            return true;
        }
    }
}