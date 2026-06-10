using System;
using System.Threading.Tasks;
using Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PI.Shared.Services;

namespace Services
{
    public class DirectLoaderService : AbstractTransferService
    {
        private readonly LoaderService _loader;

        public DirectLoaderService(
            ILogger<DirectLoaderService> logger,
            IConfiguration configuration,
            IFileStorageService fileStorageService,
            // IAPMService aPMService,
            ISingerConfigAdapter adapter,
            LoaderService loaderService
            ) : base(logger, configuration, fileStorageService, adapter)
        {
            this._loader = loaderService;
        }

        // => Publish($"{RoutePrefix}.{configId}.extract.{tag}.init", "start load");
        public override async Task InitLoadAsync(Guid configId, string tag)
        {
            await _loader.InitWorkItemAsync(configId, tag);
        }

        // => Publish($"{RoutePrefix}.{configId}.extract.{timeStamp}", line);
        public override async Task OnDataAsync(Guid configId, string tag, string line)
        {
            // extract messages
            JObject json = JsonConvert.DeserializeObject<JObject>(line);
            var type = json.GetValue("type").Value<string>();

            var workItem = _loader.GetWorkItem(configId, tag);
            if (workItem == null)
            {
                workItem = await _loader.InitWorkItemAsync(configId, tag);
            }

            var result = type switch
            {
                "STATE" => await _loader.UpdateStateAsync(workItem, json),
                "RECORD" => await _loader.LoadRecordAsync(workItem, json, null),
                "SCHEMA" => true,
                "ACTIVATE_VERSION" => true,
                _ => true
            };
        }

        // => Publish($"{RoutePrefix}.{configId}.extract.{tag}.end", "end load");
        public override async Task EndLoadAsync(Guid configId, string tag)
        {
            await _loader.ExtractEndAsync(configId, tag);
        }
    }
}