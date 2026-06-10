using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Crochik.Messaging;

public class RpcClient : IDisposable
{
    private const string ReplyToQueue = "amq.rabbitmq.reply-to";
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new();

    public RpcClient()
    {
        var factory = new ConnectionFactory 
        { 
            HostName = "localhost",
            // Vital for surviving blips
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true 
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Direct Reply-To requires a consumer on this specific name
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
        {
            var correlationId = ea.BasicProperties.CorrelationId;
            if (correlationId != null && _pendingRequests.TryRemove(correlationId, out var tcs))
            {
                var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                // RunContinuationsAsynchronously is key to prevent deadlocks on the consumer thread
                tcs.TrySetResult(response);
            }
        };

        // autoAck: true is mandatory for Direct Reply-To
        _channel.BasicConsume(queue: ReplyToQueue, autoAck: true, consumer: consumer);
    }

    public Task<string> CallAsync(string message, string targetQueue, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[correlationId] = tcs;

        var props = _channel.CreateBasicProperties();
        props.CorrelationId = correlationId;
        props.ReplyTo = ReplyToQueue;

        var body = Encoding.UTF8.GetBytes(message);

        // In 6.8.1, Publish is synchronous
        _channel.BasicPublish(
            exchange: "", 
            routingKey: targetQueue, 
            basicProperties: props, 
            body: body);

        // Cleanup if the user cancels or the call times out
        ct.Register(() => _pendingRequests.TryRemove(correlationId, out _));

        return tcs.Task;
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}