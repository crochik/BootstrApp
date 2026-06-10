using System;
using System.IO;
using System.Threading.Tasks;
using Adapters;
using Crochik.Logging;
using Crochik.NET.APM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;
using Newtonsoft.Json;
using PI.Shared.Services;

namespace Services
{
    public class ExtractService
    {
        private string BasePath => _config.LocalPath;
        private string Bucket => _config.Bucket;

        private readonly ILogger<ExtractService> _logger;
        private readonly ISingerConfigAdapter _configAdapter;
        private readonly IFileStorageService _fileStorageService;
        // private readonly IAPMService _apmService;
        private readonly ETLConfig _config;

        public ExtractService(
            ILogger<ExtractService> logger,
            IConfiguration configuration,
            ISingerConfigAdapter configAdapter,
            ITransferService loadService,
            IFileStorageService fileStorageService
            )
        {
            this._logger = logger;
            this._configAdapter = configAdapter;
            this._fileStorageService = fileStorageService;
            // this._apmService = apmService;
            this._config = configuration.GetSection(nameof(ExtractService)).Get<ETLConfig>();
        }

        private void OnStatus(SingerJob job, string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            if (!line.StartsWith("INFO "))
            {
                OnInfo(job, line);
                return;
            }

            if (line.StartsWith("INFO METRIC: "))
            {
                var metric = line.Substring("INFO METRIC: ".Length);
                OnMetric(job, metric);
                return;
            }

            var status = line.Substring("INFO ".Length);
            OnInfo(job, status);
        }

        public void OnInfo(SingerJob job, string line)
        {
            // hack to avoid writing token
            if ( line?.IndexOf(".salesforce.com/services/oauth2/token")>0) return;

            _logger.LogInformation(line);

            // _messageBroker.PublishAsync($"{RoutePrefix}.{configId}.extract.{timeStamp}.status", line);

            _configAdapter.AddToExtractLogAsync(job.Id, line).Wait();
        }

        public void OnMetric(SingerJob job, string line)
        {
            // _messageBroker.PublishAsync($"{RoutePrefix}.{configId}.extract.{timeStamp}.metric", line, contentType: "application/json");

            var metric = JsonConvert.DeserializeObject<SingerMetricMessage>(line);
            _configAdapter.AddAsync(job.Id, metric).Wait();
        }

        // private void OnOutput(Guid configId, string timeStamp, string line)
        // {
        //     if (string.IsNullOrEmpty(line)) return;
        //     Logger.LogDebug("{configId}: {line}", configId, line);

        //     _loadService.OnDataAsync(configId, timeStamp, line).Wait();
        // }

        private void SaveJson(string path, object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            using (var file = File.CreateText(path))
            {
                file.Write(json);
            }
        }

        public string GetTmpFolder(SingerJob job) => Path.Combine(BasePath, job.ConfigId.ToString(), job.Tag);
        public void RemoveTmpFolder(SingerJob job)
        {
            if (job == null) return;

            var path = GetTmpFolder(job);
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete temp folder");
            }
        }

        public async Task<(SingerJob Job, string Error)> ExtractAsync(SingerImportConfig config)
        {
            // using var apm = _apmService.StartTransaction("Extract", $"Extract {config.Id}");
            using var scope = _logger.AddScope(new
            {
                ConfigId = config.Id
            });

            _logger.LogInformation("Starting Extract for {configId}", config.Id);

            var extract = await _configAdapter.InitExtractAsync(config);

            // apm.Context = new
            // {
            //     extract.ConfigId,
            //     JobId = extract.Id,
            //     extract.Tag
            // };

            var workingFolder = GetTmpFolder(extract);
            Directory.CreateDirectory(workingFolder);

            var exec = new ExternalProcessExec(_logger)
            {
                WorkingDirectory = workingFolder,
                OutputFile = "extract.jsonl",
                ErrorFile = "extract.txt",
                OnError = (line) => OnStatus(extract, line),
                // OnOutput = (line) => OnOutput(configId, extract.Tag, line),
            };

            _logger.LogDebug("Using {workingFolder} for {jobId}", workingFolder, extract.Id);

            switch (config.TapConfig)
            {
                case SalesforceTapConfig salesforce:
                    exec.FilePath = "tap-salesforce";
                    exec.Arguments = "-c config.json --properties catalog.json";
                    break;

                default:
                    return (extract, "Tap not implemented yet");
            }

            try
            {
                // save config
                SaveJson(Path.Combine(exec.WorkingDirectory, "config.json"), config.TapConfig);
                _logger.LogDebug("Saved config.json");

                // download catalog
                await _fileStorageService.DownloadAsync(Bucket, $"{config.Id}/catalog.json", Path.Combine(exec.WorkingDirectory, "catalog.json"));
                _logger.LogDebug("Downloaded catalog.json");

                // run
                if (extract.InitialState != null)
                {
                    SaveJson(Path.Combine(exec.WorkingDirectory, "state.json"), extract.InitialState);
                    exec.Arguments += " -s state.json";
                }

                if (!exec.Run())
                {
                    _logger.LogError("Failed to extract {configId}", config.Id);
                    return (extract, "Failed to extract");
                }

                _logger.LogDebug("Finished process");

                var prefix = extract.StartedOn.ToString("yyyy/MM/dd");

                // upload files 
                await _fileStorageService.UploadAsync(
                    inputPath: Path.Combine(exec.WorkingDirectory, exec.OutputFile),
                    contentType: "application/jsonl",
                    bucket: Bucket,
                    path: $"{prefix}/{config.Id}/{extract.Tag}.jsonl"
                );

                _logger.LogDebug("Uploaded data");

                await _fileStorageService.UploadAsync(
                    inputPath: Path.Combine(exec.WorkingDirectory, exec.ErrorFile),
                    contentType: "text/plain",
                    bucket: Bucket,
                    path: $"{prefix}/{config.Id}/{extract.Tag}.txt"
                );

                _logger.LogDebug("Uploaded log");

                await _configAdapter.EndExtractAsync(extract);

                _logger.LogDebug("Done with Extraction");

                return (extract, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to Extract");
                return (extract, ex.Message);
            }
        }
    }

    public class ETLConfig
    {
        public string LocalPath { get; set; }
        public string Bucket { get; set; }
    }
}