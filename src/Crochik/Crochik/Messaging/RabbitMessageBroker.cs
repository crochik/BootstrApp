using System;
using System.Reflection;
using System.Threading.Tasks;
using Crochik.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RabbitMQ.Client;

namespace Crochik.Messaging;

class CamelCaseExceptDictionaryKeysResolver : CamelCasePropertyNamesContractResolver
{
    protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
    {
        JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);
        contract.DictionaryKeyResolver = propertyName => propertyName;
        return contract;
    }
}

public class RabbitMessageBroker : IMessageBroker
{
    public const string DefaultSection = "MessageQueue";

    private static readonly JsonSerializerSettings _settings = new()
    {
        ContractResolver = new CamelCaseExceptDictionaryKeysResolver(),
        DefaultValueHandling = DefaultValueHandling.Ignore
    };

    private readonly ILogger<RabbitMessageBroker> _logger;
    private readonly IOptions<Options> _options;
    private Options Configuration => _options.Value;
    private readonly IConnection _connection;
    private IModel _writeChannel;
    public string DefaultExchangeName => Configuration.ExchangeName;

    public RabbitMessageBroker(
        ILogger<RabbitMessageBroker> logger,
        IOptions<Options> options
    )
    {
        _logger = logger;
        _options = options;

        var factory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(1),
            Uri = new Uri(Configuration.Url),
            DispatchConsumersAsync = Configuration.DispatchConsumersAsync,
            ConsumerDispatchConcurrency = Configuration.ConsumerDispatchConcurrency ?? 1, 
        };

        try
        {
            if (Configuration.Hosts != null)
            {
                _logger.LogInformation("Connecting RabbitMQ to {Hosts}", string.Join(", ", Configuration.Hosts));
                _connection = factory.CreateConnection(Configuration.Hosts, Configuration.ConnectionName);
            }
            else
            {
                _logger.LogInformation("Connecting RabbitMQ to {HostName}", factory.HostName);
                _connection = factory.CreateConnection(Configuration.ConnectionName);
            }

            _connection.CallbackException += (sender, args) =>
            {
                _logger.LogError(args.Exception, "Connection Exception: {Detail}", args.Detail);
            };

            _logger.LogInformation("RabbitMQ Connected to {HostName}:{Port} ({ConsumerDispatchConcurrency})", _connection.Endpoint.HostName, _connection.Endpoint.Port, Configuration.ConsumerDispatchConcurrency);

            CreateChannel();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to connect to RabbitMQ");
            throw;
        }
    }

    private void CreateChannel()
    {
        _logger.LogInformation("Create Channel: {Exchange}", Configuration.ExchangeName);

        if (_writeChannel != null)
        {
            try
            {
                _logger.LogInformation("Try to close channel before recreating it");
                _writeChannel.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close channel");
            }
        }

        if (_connection.IsOpen)
        {
            _logger.LogInformation("Connection is closed!");
        }

        _writeChannel = _connection.CreateModel();
        _writeChannel.CallbackException += (sender, args) =>
        {
            _logger.LogError(args.Exception, "Write Channel Exception");
        };

        _writeChannel.ExchangeDeclare(
            exchange: Configuration.ExchangeName,
            type: Configuration.ExchangeType,
            durable: Configuration.Durable
        );

        _logger.LogInformation("Channel created");
    }

    public Task PublishAsync(string topic, string body, string exchangeName = null, string contentType = null)
    {
        Publish(topic, body, exchangeName, contentType);
        return Task.CompletedTask;
    }

    public void Publish(string topic, string body, string exchangeName = null, string contentType = null, string objectType = null)
    {
        using var scope = _logger.AddScope(new
        {
            Topic = topic,
            ExchangeName = exchangeName,
            ContentType = contentType,
            ObjectType = objectType,
        });

        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);

        IBasicProperties props = _writeChannel.CreateBasicProperties();
        props.ContentType = contentType ?? "text/plain";
        if (!string.IsNullOrEmpty(contentType) && !string.IsNullOrEmpty(objectType))
        {
            props.Type = objectType;
        }

        props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        publish();

        void publish(bool retry = true)
        {
            try
            {
                lock (_writeChannel)
                {
                    if (_writeChannel.IsClosed)
                    {
                        CreateChannel();
                    }

                    _writeChannel.BasicPublish(exchange: exchangeName ?? Configuration.ExchangeName,
                        routingKey: topic,
                        basicProperties: props,
                        body: bodyBytes);
                }

                // _logger.LogInformation("Message Published");
            }
            catch (RabbitMQ.Client.Exceptions.AlreadyClosedException ex)
            {
                if (retry)
                {
                    _logger.LogWarning(ex, "AlreadyClosed: Failed to publish message, retry");

                    publish(false);
                    return;
                }

                _logger.LogError(ex, "Failed to publish message after retry");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message");
                throw;
            }
        }
    }

    public void Publish(string topic, IMessageBody body, string exchangeName = null)
    {
        var json = JsonConvert.SerializeObject(body, _settings);

        Publish(topic, json, exchangeName, "application/json", body.GetType().FullName);
    }

    public Task PublishAsync(string topic, IMessageBody body, string exchangeName = null)
    {
        Publish(topic, body, exchangeName);

        return Task.CompletedTask;
    }

    public IMessageQueue CreateSubscription(QueueConfig queueConfig)
    {
        if (_connection.IsOpen)
        {
            _logger.LogInformation("Connection is closed!");
        }

        var queue = new RabbitMessageQueue(_logger, _connection, Configuration, queueConfig);

        return queue;
    }

    public void Bind(IMessageQueue queue, string routingKey, string exchangeName = null)
    {
        _writeChannel.QueueBind(queue.Name, exchangeName ?? DefaultExchangeName, routingKey);
    }

    public class Options
    {
        public static Options Get(IConfiguration configuration)
            => configuration.GetSection(DefaultSection).Get<Options>();

        public string Url { get; set; }
        public string ExchangeName { get; set; }
        public string ExchangeType { get; set; }
        public bool Durable { get; set; } = true;
        public string[] Hosts { get; set; }

        /// <summary>
        /// Whether to use the async consumer
        /// </summary>
        public bool DispatchConsumersAsync { get; set; } = true;
            
        /// <summary>
        /// Number of dispatchers for the connection (default = 1) 
        /// </summary>
        public int? ConsumerDispatchConcurrency { get; set; }

        public string ConnectionName { get; } = Assembly.GetEntryAssembly().GetName().Name;
    }
}

public static class IServiceCollectionExtensions
{
    public static IServiceCollection ConfigureRabbitMqService(this IServiceCollection services, IConfiguration configuration)
        => services.Configure<RabbitMessageBroker.Options>(configuration.GetSection(RabbitMessageBroker.DefaultSection));
}

public static class IConfigurationExtensions
{
    public static bool IsRabbitMqServiceConfigured(this IConfiguration configuration)
    {
        var options = RabbitMessageBroker.Options.Get(configuration);
        return options?.Url?.StartsWith("amqp://") ?? false;
    }
}