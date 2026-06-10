using System;
using System.Threading.Tasks;
using Adapters;
using Crochik.Messaging;
using Crochik.NET.APM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.Services;

namespace Services
{
    public class RabbitMqTransferService : AbstractTransferService
    {
        private readonly IMessageBroker _messageBroker;
        private string RoutePrefix => "singer";

        public RabbitMqTransferService(
            ILogger<RabbitMqTransferService> logger,
            IConfiguration configuration,
            IMessageBroker messageBroker,
            IFileStorageService fileStorageService,
            // IAPMService apmService,
            ISingerConfigAdapter adapter
            ) : base(logger, configuration, fileStorageService, adapter)
        {
            this._messageBroker = messageBroker;
        }

        public override Task OnDataAsync(Guid configId, string timeStamp, string line)
            => Publish($"{RoutePrefix}.{configId}.extract.{timeStamp}", line);

        public override Task InitLoadAsync(Guid configId, string tag)
            => Publish($"{RoutePrefix}.{configId}.extract.{tag}.init", "start load");

        public override Task EndLoadAsync(Guid configId, string tag)
            => Publish($"{RoutePrefix}.{configId}.extract.{tag}.end", "end load");

        private Task Publish(string route, string line, string contentType = null)
            => _messageBroker.PublishAsync(route, line, contentType: contentType);
    }
}