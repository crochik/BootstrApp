using System;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Crochik.Messaging;

public class RabbitSubscription : ISubscription
{
    private ITypeMapper _typeMapper;

    public IModel Channel { get; init; }
    public string QueueName { get; init; }
    public bool AutoAck { get; init; }
    public Action<IMessage> Action { get; init; }
    
    private EventingBasicConsumer _consumer = null;
    private string _tag = null;
    private readonly ILogger _logger;

    public RabbitSubscription(ILogger logger)
    {
        _logger = logger;
    }

    public bool Start(ITypeMapper typeMapper = null)
    {
        if (_tag != null) return false;

        _typeMapper = typeMapper;
        _consumer = new EventingBasicConsumer(Channel);
        _consumer.Received += OnMessageReceived;
        _tag = Channel.BasicConsume(QueueName, AutoAck, _consumer);

        return true;
    }

    private void OnMessageReceived(object channel, BasicDeliverEventArgs ea)
    {
        string bodyStr = System.Text.Encoding.UTF8.GetString(ea.Body.ToArray());
        object body = bodyStr;

        if (!string.IsNullOrEmpty(ea?.BasicProperties.Type) && ea?.BasicProperties.Type != "string")
        {
            Type type = _typeMapper?[ea.BasicProperties.Type];
            if (type != null)
            {
                try
                {
                    body = JsonConvert.DeserializeObject(bodyStr, type);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{RoutingKey}: Error deserializing {Type} with {MessageId} from {Timestamp}: {Body}", ea.RoutingKey, ea.BasicProperties.Type, ea.BasicProperties.MessageId, ea.BasicProperties.Timestamp, bodyStr);
                    return;
                }
            }
        }

        var ev = new RabbitQueueEvent(ea)
        {
            Channel = Channel,
            Body = body,
            WasAcknowledged = AutoAck
        };

        Action.Invoke(ev);
    }

    public bool Stop()
    {
        if (_tag == null) return false;
        Channel.BasicCancel(_tag);
        _tag = null;

        return true;
    }
}