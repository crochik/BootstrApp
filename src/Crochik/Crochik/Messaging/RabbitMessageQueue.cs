using System;
using System.Threading.Tasks;
using Crochik.Logging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Crochik.Messaging;

public class RabbitMessageQueue : IMessageQueue
{
    private readonly ILogger _logger;
    private readonly IModel _channel;
    private readonly QueueDeclareOk _declare;
    private readonly QueueConfig _queueConfig;
    private readonly RabbitMessageBroker.Options _connectionOptions;

    public string Name => _queueConfig.QueueName ?? "";
    public ISubscription Subscription { get; private set; }

    public RabbitMessageQueue(ILogger logger, IConnection connection, RabbitMessageBroker.Options options, QueueConfig queueConfig)
    {
        _logger = logger;
        _connectionOptions = options;
        _queueConfig = queueConfig;

        var name = queueConfig.QueueName;
        var durable = queueConfig.Durable;
        var exclusive = queueConfig.Exclusive;
        var autoDelete = queueConfig.AutoDelete;
        var extraArgs = queueConfig.ExtraArgs;

        using var scope = _logger.AddScope(new
        {
            Name = name,
            Durable = durable,
            Exclusice = exclusive,
            AutoDelete = autoDelete,
        });
        
        _logger.LogInformation("Create Subscription {Name}: {PrefetchCount} ({ConcurrencyMax})", name, queueConfig.PrefetchCount, queueConfig.ConcurrencyMax);

        _channel = connection.CreateModel();

        _channel.BasicQos(0, queueConfig.PrefetchCount ?? 100, false); //???

        _channel.CallbackException += (sender, args) =>
        {
            using var scope = _logger.AddScope(new
            {
                Name = name,
                Durable = durable,
                Exclusice = exclusive,
                AutoDelete = autoDelete,
            });

            _logger.LogError(args.Exception, "Read Channel Exception");
        };

        _channel.ExchangeDeclare(
            exchange: options.ExchangeName,
            type: options.ExchangeType,
            durable: options.Durable
        );

        _declare = _channel.QueueDeclare(name ?? "", durable: durable, exclusive: exclusive, autoDelete: autoDelete, arguments: extraArgs);
    }

    public ISubscription Subscribe(Func<IMessage, Task> handler, bool autoAck = true)
    {
        using var scope = _logger.AddScope(new
        {
            Name,
        });

        if (_connectionOptions.DispatchConsumersAsync)
        {
            _logger.LogInformation("Create Async subscription");

            Subscription = new AsyncRabbitSubscription(_logger, _queueConfig)
            {
                Channel = _channel,
                QueueName = Name,
                AutoAck = autoAck,
                Action = handler,
            };

            return Subscription;
        }
        else
        {
            // NOT USED ANYMORE?
            // ... 
            _logger.LogInformation("Create subscription");

            Subscription = new RabbitSubscription(_logger)
            {
                Channel = _channel,
                QueueName = Name,
                AutoAck = autoAck,
                Action = (evt) =>
                {
                    try
                    {
                        handler(evt).Wait();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing event");
                    }
                }
            };

            return Subscription;
        }
    }

    public void StartSubscription(Func<IMessage, Task> onMessageReceived, TypeMapper messageTypeMapper)
    {
        if (Subscription != null) return;
        
        _logger.LogInformation("Start listening to {QueueName}", _queueConfig.QueueName);
        
        Subscription = Subscribe(onMessageReceived, _queueConfig.AutoAck);
        Subscription.Start(messageTypeMapper);
    }

    public void StopSubscription()
    {
        if (Subscription == null) return;

        _logger.LogInformation("Stop listening to {QueueName}", _queueConfig?.QueueName);
        try
        {
            Subscription.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close subscription: {QueueName}", _queueConfig?.QueueName);
        }
        finally
        {
            Subscription = null;
        }
    }
}