using System;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Crochik.Messaging;

/// <summary>
/// Will process messages in parallel (asynchronously)
/// - The connection has to have ConsumerDispatchConcurrency set to more than to process more than one message at a time
/// - It will process up to PrefetchCount/ConsumerDispatchConcurrency messages
/// </summary>
public class AsyncRabbitSubscription(ILogger logger, QueueConfig config) : ISubscription
{
    private ITypeMapper _typeMapper;

    public IModel Channel { get; init; }
    public string QueueName { get; init; }
    public bool AutoAck { get; init; }
    public Func<IMessage, Task> Action { get; init; }
    private AsyncEventingBasicConsumer _consumer = null;
    private string _tag = null;
    private SemaphoreSlim _semaphore = null;

    public bool Start(ITypeMapper typeMapper = null)
    {
        if (_tag != null) return false;
        if (config.ConcurrencyMax.HasValue)
        {
            _semaphore = new SemaphoreSlim(config.ConcurrencyMax.Value, config.ConcurrencyMax.Value);
        }

        _typeMapper = typeMapper;
        _consumer = new AsyncEventingBasicConsumer(Channel);
        _consumer.Received += OnMessageReceived;

        _tag = Channel.BasicConsume(QueueName, AutoAck, _consumer);

        return true;
    }

    private async Task OnMessageReceived(object channel, BasicDeliverEventArgs ea)
    {
        using var scope = logger.AddScope(new
        {
            QueueName,
            ea.RoutingKey,
            ea.BasicProperties.Type,
            ea.BasicProperties.MessageId,
            ea.BasicProperties.Timestamp.UnixTime,
        });

        try
        {
            if (_semaphore != null)
            {
                // limit number of messages being processed
                await _semaphore.WaitAsync();
            }

            logger.LogInformation("Start Processing Event");
            await ProcessMessageReceived(channel, ea);
            logger.LogInformation("Finished Processing Event");
        }
        catch (Exception ex)
        {
            if (config.AutoRejectOnException)
            {
                Channel.BasicReject(ea.DeliveryTag, config.Requeue);
            }

            logger.LogError(ex, "Error processing event");
        }
        finally
        {
            _semaphore?.Release();
        }
    }

    private async Task ProcessMessageReceived(object channel, BasicDeliverEventArgs ea)
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
                    // TODO: this message will just "hang" the queue
                    // should probably reject (w/o requeueing?)
                    // ...
                    logger.LogError(ex, "{RoutingKey}: Error deserializing {Type} with {MessageId} from {Timestamp}: {Body}", ea.RoutingKey, ea.BasicProperties.Type, ea.BasicProperties.MessageId, ea.BasicProperties.Timestamp, bodyStr);

                    // currently do "nothing" 
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

        await Action.Invoke(ev);
    }

    public bool Stop()
    {
        if (_tag == null) return false;
        Channel.BasicCancel(_tag);
        _tag = null;

        return true;
    }
}