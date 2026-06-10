using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Threading.Tasks;
using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;
using CometD.NetCore.Client;
using CometD.NetCore.Client.Extension;
using CometD.NetCore.Client.Transport;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Stream;

public class SubscriberService : IMessageListener, ILifetimeService
{
    private readonly SalesforceService _salesforce;

    private readonly ILogger<SubscriberService> _logger;
    private BayeuxClient _client;
    private ErrorExtension _errorExtension;
    private ReplayExtension _replayIdExtension;

    public SubscriberService(
        ILogger<SubscriberService> logger,
        SalesforceService salesforce
    )
    {
        _logger = logger;
        _salesforce = salesforce;
    }

    public void OnMessage(IClientSessionChannel channel, IMessage message)
    {
        _logger.LogDebug(message.Json);
    }

    public void Start()
    {
        ConnectAsync().Wait();
    }

    public async Task ConnectAsync()
    {
        var context = new AccountContext(AccountIds.FCI);
        var token = await _salesforce.GetTokenAsync(context, true);

        try
        {
            int readTimeOut = 120000;
            var options = new Dictionary<string, object>
            {
                { ClientTransport.TIMEOUT_OPTION, readTimeOut }
            };

            // var collection = new NameValueCollection();
            // collection.Add(HttpRequestHeader.Authorization.ToString(), $"OAuth {token}");
            // var transport = new LongPollingTransport(options, new NameValueCollection { collection });


            // Salesforce socket timeout during connection(CometD session) = 110 seconds
            var _readTimeOut = 120 * 1000;
            var headers = new NameValueCollection { { nameof(HttpRequestHeader.Authorization), $"OAuth {token.Token.AccessToken}" } };
            var transportOptions = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { ClientTransport.TIMEOUT_OPTION, _readTimeOut },
                { ClientTransport.MAX_NETWORK_DELAY_OPTION, _readTimeOut }
            };

            var transport = new LongPollingTransport(transportOptions, headers);

            var streamingEndpointURI = "/cometd/43.0";
            var serverUri = new Uri("https://fcifloors--fcistaging.my.salesforce.com");
            var endpoint = $"{serverUri.Scheme}://{serverUri.Host}{streamingEndpointURI}";
            _client = new BayeuxClient(endpoint, new[] { transport });

            // adds logging and also raises an event to process reconnection to the server.
            _errorExtension = new ErrorExtension();
            _errorExtension.ConnectionError += ErrorExtension_ConnectionError;
            _errorExtension.ConnectionException += ErrorExtension_ConnectionException;
            _errorExtension.ConnectionMessage += ErrorExtension_ConnectionMessage;
            _client.AddExtension(_errorExtension);

            _replayIdExtension = new ReplayExtension();
            _client.AddExtension(_replayIdExtension);


            var channel = _client.GetChannel("/**");
            channel.AddListener(new ChannelListener());

            // https://github.com/nthachus/CometD.NET#prerequisites
            // use https://workbench.developerforce.com/streaming.php
            // to create topic 

            // PushTopic pushTopic = new PushTopic();
            // pushTopic.Name = 'LeadUpdates';
            // pushTopic.Query = 'SELECT Id, Name FROM Lead';
            // pushTopic.ApiVersion = 43.0;
            // insert pushTopic;

            var timeout = 1000;
            _logger.LogDebug("Handshaking...");
            _client.Handshake();
            _client.WaitFor(timeout, new[] { BayeuxClient.State.CONNECTED });
            _logger.LogDebug("Connected");

            _client.GetChannel("/topic/LeadUpdates").Subscribe(this);

            _logger.LogInformation($"Connected: {_client.Connected}");

            //Close the connection
            // pushTopicConnection.Disconect();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting");
        }
    }

    private void ErrorExtension_ConnectionMessage(object sender, string e)
    {
        _logger.LogInformation($"Connecting: {e}");
    }

    private void ErrorExtension_ConnectionException(object sender, Exception e)
    {
        _logger.LogError(e, "Failed to connect");
    }

    private void ErrorExtension_ConnectionError(object sender, string e)
    {
        _logger.LogError($"Connection Error: {e}");
    }

    public void Stop()
    {
    }

    public class ChannelListener : IClientSessionChannelListener, IMessageListener
    {
        public void OnMessage(IClientSessionChannel channel, IMessage message)
        {
            System.Console.WriteLine(message.Json);
        }
    }
}