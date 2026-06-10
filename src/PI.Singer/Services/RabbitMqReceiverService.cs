using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adapters;
using AutoMapper;
using Crochik.Logging;
using Crochik.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PI.Shared.Data.Adapters;

namespace Services
{
    public class RabbitMqReceiverService : AbstractMessageQueueService
    {
        private readonly IMapper _mapper;
        private readonly LoaderService _loader;
        private readonly ILeadTypeAdapter _leadTypeAdapter;
        private readonly IEntityIdentityAdapter _entityAdapter;
        private readonly ISingerConfigAdapter _configAdapter;

        public RabbitMqReceiverService(
            ILogger<RabbitMqReceiverService> logger,
            IMapper mapper,
            IConfiguration configuration,
            IMessageBroker messageBroker,
            // IAPMService apmService,
            LoaderService loader,
            IEntityIdentityAdapter entityAdapter,
            ILeadTypeAdapter leadTypeAdapter,
            ISingerConfigAdapter configAdapter
            ) : base(logger, configuration, messageBroker)
        {
            this._configAdapter = configAdapter;
            this._entityAdapter = entityAdapter;
            this._leadTypeAdapter = leadTypeAdapter;
            this._mapper = mapper;
            this._loader = loader;
        }

        protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
        {
            MessageBroker.Bind(messageQueue, "singer.*.extract.#");
        }

        protected override async Task OnMessageAsync(IMessage evt)
        {
            if (await ProcessMessageAsync(evt)) evt.Acknowledge();
        }

        private async Task<bool> ProcessMessageAsync(IMessage evt)
        {
            var parts = evt.RoutingKey.Split('.');
            if (!Guid.TryParse(parts[1], out var configId) || parts.Length < 4 || parts.Length > 5 || parts[2] != "extract")
            {
                Logger.LogError("Invalid {route}", evt.RoutingKey);
                return false;
            }

            var bodyObj = evt.Body;
            if (!(bodyObj is string body))
            {
                Logger.LogError("Unexpected {bodyType}", bodyObj.GetType().Name);
                return false;
            }

            var tag = parts[3];
            if (parts.Length == 4)
            {
                // extract messages
                JObject json = JsonConvert.DeserializeObject<JObject>(body);
                var type = json.GetValue("type").Value<string>();

                var workItem = _loader.GetWorkItem(configId, tag);
                if (workItem == null)
                {
                    workItem = await _loader.InitWorkItemAsync(configId, tag);
                }

                return type switch
                {
                    "STATE" => await _loader.UpdateStateAsync(workItem, json),
                    "RECORD" => await _loader.LoadRecordAsync(workItem, json, evt.RoutingKey),
                    "SCHEMA" => true,
                    "ACTIVATE_VERSION" => true,
                    _ => true
                };
            }

            return parts[4] switch
            {
                "init" => await ExtractInitAsync(configId, tag),
                "status" => await ExtractLogAsync(configId, tag, body),
                "metric" => await ExtractMetricAsync(configId, tag, body),
                "end" => await _loader.ExtractEndAsync(configId, tag),
                _ => false
            };
        }

        private async Task<bool> ExtractInitAsync(Guid configId, string tag)
        {
            await _loader.InitWorkItemAsync(configId, tag);
            return true;
        }

        private async Task<bool> ExtractMetricAsync(Guid configId, string tag, string body)
        {
            using var scope = Logger.AddScope(new
            {
                ConfigId = configId,
                Tag = tag
            });

            var workItem = _loader.GetWorkItem(configId, tag);
            var metric = JsonConvert.DeserializeObject<SingerMetricMessage>(body);

            await _configAdapter.AddAsync(workItem.Import.Id, metric);

            return true;
        }

        private async Task<bool> ExtractLogAsync(Guid configId, string tag, string body)
        {
            using var scope = Logger.AddScope(new
            {
                ConfigId = configId,
                Tag = tag
            });

            Logger.LogInformation(body);

            var workItem = _loader.GetWorkItem(configId, tag);
            await _configAdapter.AddToExtractLogAsync(workItem.Import.Id, body);

            return true;
        }
    }

    public class WorkItem
    {
        public SingerImportConfig Config { get; set; }
        public Dictionary<string, SingerStreamConfig> CachedStreams { get; set; }
        public SingerJob Import { get; set; }

        public SingerStreamConfig this[string name]
        {
            get => CachedStreams.TryGetValue(name, out var stream) ? stream :
                (Config.Streams.TryGetValue(name, out var stream2) ? stream2 : null);
        }
    }
}