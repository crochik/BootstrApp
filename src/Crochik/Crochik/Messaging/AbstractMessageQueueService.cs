using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.NET.APM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Crochik.Messaging;

public abstract class AbstractMessageQueueService
{
    protected ILogger<AbstractMessageQueueService> Logger { get; }
    protected IMessageBroker MessageBroker { get; }
    // protected IAPMService ApmService { get; }
    
    private IMessageQueue _queue;
    private TypeMapper MessageTypeMapper { get; set; } = new TypeMapper();

    protected abstract Task OnMessageAsync(IMessage evt);
    protected abstract void Init(IMessageQueue messageQueue, TypeMapper mapper);

    private readonly QueueConfig _queueConfig;

    protected AbstractMessageQueueService(
        ILogger<AbstractMessageQueueService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker
        // IAPMService apmService
    )
    {
        Logger = logger;
        MessageBroker = messageBroker;
        // ApmService = apmService;

        _queueConfig = configuration.GetSection(this.GetType().Name).Get<QueueConfig>();
    }

    public virtual void Start()
    {
        if (string.IsNullOrEmpty(_queueConfig?.QueueName))
        {
            Logger.LogWarning("Missing QueueName from config, can't subscribe: {Service}", GetType().Name);
            return;
        }

        Logger.LogInformation("Starting {Service}: create {Queue}", GetType().Name, _queueConfig.QueueName);

        _queue = MessageBroker.CreateSubscription(_queueConfig);

        if (_queueConfig.Bindings != null)
        {
            foreach (var binding in _queueConfig.Bindings)
            {
                MessageBroker.Bind(_queue, binding);
            }
        }

        Init(_queue, MessageTypeMapper);

        _queue.StartSubscription(OnMessageReceivedAsync, MessageTypeMapper);
    }

    public virtual void Stop()
    {
        _queue?.StopSubscription();
    }

    private async Task OnMessageReceivedAsync(IMessage evt)
    {
        var requestId = Guid.NewGuid().ToString();
        using var scope = Logger.AddScope(new
        {
            evt.RoutingKey,
            evt.BodyType,
            RequestId = requestId,
        });

        // using var apm = ApmService?.StartTransaction("Message", $"{GetType().Name} {evt.Body.GetType().FullName}");
        // apm.Context = new
        // {
        //     evt.RoutingKey,
        //     RequestId = requestId,
        // };

        await OnMessageAsync(evt);
    }
}

public class QueueConfig
{
    public string QueueName { get; init; }
    public string[] Bindings { get; init; }
    public bool Durable { get; init; } = true;
    public bool Exclusive { get; init; } = false;
    public bool AutoDelete { get; init; } = false;
    public bool AutoAck { get; init; } = false;

    /// <summary>
    /// Auto nack failed messages 
    /// </summary>
    public bool AutoRejectOnException { get; init; } = false;

    /// <summary>
    /// When auto nack, requeue or simply discard.
    /// </summary>
    public bool Requeue { get; init; } = false;
    
    /// <summary>
    /// Number of messages per batch of fetch
    /// - should be more than 1 for parallel processing
    /// - will default to 100 if omitted (old behavior) 
    /// </summary>
    public ushort? PrefetchCount { get; init; } = null;

    /// <summary>
    /// Max concurrent tasks processing 
    /// </summary>
    public ushort? ConcurrencyMax { get; init; } 

    // additional arguments used in queue declaration
    public Dictionary<string, object> ExtraArgs { get; init; }
}