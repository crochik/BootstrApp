using System;
using System.IO;
using System.Threading.Tasks;
using Adapters;
using Crochik.NET.APM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services
{
    public abstract class AbstractTransferService : ITransferService
    {
        protected readonly ILogger<AbstractTransferService> _logger;
        protected readonly IFileStorageService _fileStorageService;
        // private readonly IAPMService _apmService;
        private readonly ISingerConfigAdapter _adapter;
        protected readonly ETLConfig _etlConfig;
        protected string Bucket => _etlConfig.Bucket;

        public abstract Task EndLoadAsync(Guid configId, string timeStamp);
        public abstract Task InitLoadAsync(Guid configId, string timeStamp);
        public abstract Task OnDataAsync(Guid configId, string timeStamp, string line);

        protected AbstractTransferService(
            ILogger<AbstractTransferService> logger,
            IConfiguration configuration,
            IFileStorageService fileStorageService,
            // IAPMService aPMService,
            ISingerConfigAdapter adapter
            )
        {
            this._logger = logger;
            this._fileStorageService = fileStorageService;
            // this._apmService = aPMService;
            this._adapter = adapter;
            this._etlConfig = configuration.GetSection(nameof(ExtractService)).Get<ETLConfig>();
        }

        public async Task<Result<string>> LoadAsync(string tmpFolder, SingerJob job)
        {
            // using var apm = _apmService.StartTransaction($"Load {job.ConfigId}", "Load");
            // apm.Context = new
            // {
            //     ConfigId = job.ConfigId,
            //     Tag = job.Tag
            // };

            Directory.CreateDirectory(tmpFolder);

            var localPath = Path.Combine(tmpFolder, "extract.jsonl");
            var prefix = job.StartedOn.ToString("yyyy/MM/dd");
            if (!File.Exists(localPath))
            {
                await _fileStorageService.DownloadAsync(
                    Bucket,
                    $"{prefix}/{job.ConfigId}/{job.Tag}.jsonl",
                    localPath
                );
            }

            await ProcessFileAsync(job, localPath);

            File.Delete(localPath);

            return Result.Success("Worked?");
        }

        public async Task ProcessFileAsync(SingerJob job, string localPath)
        {
            await InitLoadAsync(job.ConfigId, job.Tag);

            var reader = File.OpenText(localPath);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                await OnOutput(job.ConfigId, job.Tag, line);
            }

            await EndLoadAsync(job.ConfigId, job.Tag);
        }

        protected async Task OnOutput(Guid configId, string timeStamp, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _logger.LogDebug("{configId}: {line}", configId, line);

            await OnDataAsync(configId, timeStamp, line);
        }
    }
}