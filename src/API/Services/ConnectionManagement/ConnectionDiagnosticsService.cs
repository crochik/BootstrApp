namespace Services;

public class ConnectionDiagnosticsService(ILogger<ConnectionDiagnosticsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            
            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorker, out int maxCompletion);

            logger.LogDebug(
                "ThreadPool — Workers: {Available}/{Max}, IOCP: {IOAvail}/{IOMax}, Pending: {Pending}",
                workerThreads, maxWorker,
                completionPortThreads, maxCompletion,
                ThreadPool.PendingWorkItemCount);
                
            // GC pressure check
            var gcInfo = new
            {
                Gen0 = GC.CollectionCount(0),
                Gen1 = GC.CollectionCount(1),
                Gen2 = GC.CollectionCount(2),
                MemoryMB = GC.GetTotalMemory(false) / 1024 / 1024
            };
            
            logger.LogDebug(
                "GC — Gen0:{Gen0} Gen1:{Gen1} Gen2:{Gen2} Memory:{MemoryMB}MB",
                gcInfo.Gen0, gcInfo.Gen1, gcInfo.Gen2, gcInfo.MemoryMB);
        }
    }
}

public class RawConnectionEntry
{
    public string ConnId { get; init; } = "";
    public string RemoteEndPoint { get; init; } = "";
    public string LocalEndPoint { get; init; } = "";
    public DateTimeOffset OpenedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; set; }
    public bool HadHttpRequest { get; set; }
    public bool ClosedCleanly { get; set; }
    public string? Exception { get; set; }
}