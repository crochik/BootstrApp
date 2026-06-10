namespace Crochik.Messaging;

public interface IMessage
{
    bool WasAcknowledged { get; }

    object Body { get; }

    string RoutingKey { get; }

    string BodyType { get; }

    /// <summary>
    /// Ack message
    /// </summary>
    void Acknowledge();
}