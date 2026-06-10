using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PI.Shared.Middleware;

public class DiagnosticLoggingMiddleware(RequestDelegate next, ILogger<DiagnosticLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // 1. ThreadPool Metrics
        ThreadPool.GetMinThreads(out int minWorker, out int minIo);
        ThreadPool.GetMaxThreads(out int maxWorker, out int maxIo);
        ThreadPool.GetAvailableThreads(out int availWorker, out int availIo);
        
        int busyWorker = maxWorker - availWorker;

        // 2. Memory / GC Metrics
        var memInfo = GC.GetGCMemoryInfo();
        long totalMemoryMb = GC.GetTotalMemory(false) / 1024 / 1024;

        // 3. Only log "Warning" if threads are getting tight, otherwise Info
        var logLevel = (busyWorker > minWorker * 0.8) ? LogLevel.Warning : LogLevel.Information;

        // logger.Log(logLevel, 
        //     "DIAG [Path: {Path}] | Threads: {Busy}/{Max} (Min: {Min}) | IOCP: {AvailIo} | Mem: {Mem}MB | GC Reason: {Regen}",
        //     context.Request.Path,
        //     busyWorker, maxWorker, minWorker,
        //     availIo,
        //     totalMemoryMb,
        //     memInfo.Index);
        
        logger.Log(logLevel,
            "DIAG [Path: {Path}] | Threads: {Busy}/{Max} (Min: {Min}) | IOCP: {AvailIo} | Mem: {Mem}MB | Heap: {Heap}MB | Gen0: {G0} Gen1: {G1} Gen2: {G2}",
            context.Request.Path,
            busyWorker, maxWorker, minWorker,
            availIo,
            totalMemoryMb,
            memInfo.HeapSizeBytes / 1024 / 1024,
            GC.CollectionCount(0),   // cumulative Gen0 collections
            GC.CollectionCount(1),   // cumulative Gen1 collections
            GC.CollectionCount(2));  // cumulative Gen2 collections — most expensive

        await next(context);
    }
}