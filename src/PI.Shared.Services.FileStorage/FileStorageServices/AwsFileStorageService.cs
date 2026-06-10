using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Logging;

namespace PI.Shared.Services;

public class AwsFileStorageService : IFileStorageService
{
    public string Provider => "AWS";

    private readonly ILogger<AwsFileStorageService> _logger;
    private readonly IAmazonS3 _client;

    public AwsFileStorageService(
        ILogger<AwsFileStorageService> logger,
        AmazonS3Client s3Client
    )
    {
        _logger = logger;
        _client = s3Client;
    }
    
    public async Task DownloadAsync(string bucket, string path, string outputPath)
    {
        _logger.LogInformation("Download to {localFile} from {bucket}/{path}", outputPath, bucket, path);

        var request = new GetObjectRequest();
        request.BucketName = bucket;
        request.Key = path;
        var response = await _client.GetObjectAsync(request);
        await response.WriteResponseStreamToFileAsync(outputPath, false, default(CancellationToken));
    }

    public async Task<string> UploadAsync(string inputPath, string contentType, string bucket, string path)
    {
        _logger.LogInformation("Upload {localFile} to {bucket}/{path}", inputPath, bucket, path);

        var fileTransferUtility = new TransferUtility(_client);
        await fileTransferUtility.UploadAsync(inputPath, bucket, path);

        return $"s3://{bucket}/{path}";
    }

    public async Task<string> UploadAsync(Stream stream, string contentType, string bucket, string path, bool? allowAnonymousRead = null)
    {
        _logger.LogInformation("Upload stream to {bucket}/{path}", bucket, path);

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = stream,
            BucketName = bucket,
            Key = path,
        };

        if (allowAnonymousRead.GetValueOrDefault())
        {
            uploadRequest.CannedACL = S3CannedACL.PublicRead;
        }
        
        var fileTransferUtility = new TransferUtility(_client);
        await fileTransferUtility.UploadAsync(uploadRequest);

        return $"s3://{bucket}/{path}";
    }
}