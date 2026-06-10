using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PI.Shared.Services;

public class GcpFileStorageService : IFileStorageService
{
    public string Provider => "GCP";
        
    private readonly ILogger<GcpFileStorageService> _logger;

    public GcpFileStorageService(
        ILogger<GcpFileStorageService> logger,
        IConfiguration configuration
    )
    {
        _logger = logger;
    }

    private async Task GetDir()
    {
        using var client = await StorageClient.CreateAsync();
        var dir = client.ListObjectsAsync("singer-config", "");

        while (true)
        {
            var page = await dir.ReadPageAsync(100);
            foreach (var obj in page)
            {
                _logger.LogInformation("{name}: {type}", obj.Name, obj.ContentType);
            }

            if (string.IsNullOrEmpty(page.NextPageToken)) break;
        }
    }

    public async Task DownloadAsync(string bucket, string path, string outputPath)
    {
        using (var stream = File.Create(outputPath))
        {
            await DownladAsync(bucket, path, stream);
        }
    }

    public async Task DownladAsync(string bucket, string path, Stream stream)
    {
        // https://googleapis.dev/dotnet/Google.Cloud.Storage.V1/3.0.0-beta02/index.html
        using var client = await StorageClient.CreateAsync();
        await client.DownloadObjectAsync(bucket, path, stream);
    }

    public async Task<string> UploadAsync(string inputPath, string contentType, string bucket, string path)
    {
        using var client = await StorageClient.CreateAsync();
        using (var stream = File.Open(inputPath, FileMode.Open, FileAccess.Read))
        {
            var obj = await client.UploadObjectAsync(bucket, path, contentType, stream);
            return obj.MediaLink;
        }
    }

    public async Task<string> UploadAsync(Stream stream, string contentType, string bucket, string path, bool? allowAnonymousRead = null)
    {
        if (allowAnonymousRead.GetValueOrDefault())
        {
            throw new NotImplementedException("tbd...");
        }
            
        using var client = await StorageClient.CreateAsync();
        var obj = await client.UploadObjectAsync(bucket, path, contentType, stream);
        return obj.MediaLink;
    }

    public static string GetProjectId()
    {
        var envVar = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
        if (envVar != null)
        {
            return envVar;
        }

        // Use the service account credentials, if present.
        var googleCredential = GoogleCredential.GetApplicationDefault();
        if (googleCredential != null)
        {
            ICredential credential = googleCredential.UnderlyingCredential;
            ServiceAccountCredential serviceAccountCredential = credential as ServiceAccountCredential;
            if (serviceAccountCredential != null)
            {
                return serviceAccountCredential.ProjectId;
            }
        }

        try
        {
            // Query the metadata server.
            HttpClient http = new HttpClient();
            http.DefaultRequestHeaders.Add("Metadata-Flavor", "Google");
            http.BaseAddress = new Uri(@"http://metadata.google.internal/computeMetadata/v1/project/");
            return http.GetStringAsync("project-id").Result;
        }
        catch (AggregateException e)
            when (e.InnerException is HttpRequestException)
        {
            throw new Exception("Could not find Google project id.  " +
                                "Run this application in Google Cloud or follow these " +
                                "instructions to run locally: " +
                                "https://cloud.google.com/docs/authentication/getting-started",
                e.InnerException);
        }
    }
}