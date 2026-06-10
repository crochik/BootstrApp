using System;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Services;

namespace PI.Files.Services.Jobs;

public class ScanS3FilesJob : IRunJob
{
    private readonly MongoConnection _connection;
    private readonly RemoteFileService _service;
    public string Name => "ScanS3Files";

    public ScanS3FilesJob(MongoConnection connection, RemoteFileService service)
    {
        _connection = connection;
        _service = service;
    }
    
    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var bucketId = Guid.Parse("03f4a3cf-0aed-4a0d-ad72-293197fc3218");

        var bucket = await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, bucketId)
            .FirstOrDefaultAsync();

        var result = await _service.ListAsync(context, bucket, "salesforce/");

        return new JobResult
        {
            Message = "Not implemented",
        };
    }
}