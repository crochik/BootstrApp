namespace Crochik.Messaging
{
    public interface ISubscription
    {
        string QueueName { get; }
        bool AutoAck { get; }
        bool Start(ITypeMapper typeMapper =null);
        bool Stop();
    }
}