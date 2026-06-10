using System.Diagnostics;

namespace Services;

public class ConnectionStateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ConnectionStateMiddleware> _logger;
    private readonly ConnectionStateInspector _inspector;

    public ConnectionStateMiddleware(
        RequestDelegate next,
        ILogger<ConnectionStateMiddleware> logger,
        ConnectionStateInspector inspector)
    {
        _next = next;
        _logger = logger;
        _inspector = inspector;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Tell the raw tracker this connection made it to HTTP pipeline
        RawConnectionTracker.MarkHadHttpRequest(context.Connection.Id);

        var connectionId = context.Connection.Id;
        var path = context.Request.Path.ToString();
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        var protocol = context.Request.Protocol;

        // --- PRE-REQUEST CHECK ---
        var pre = _inspector.Inspect(connectionId);

        if (!pre.IsHealthy)
        {
            _logger.LogError(
                "[CONN_PRE] Unhealthy connection BEFORE request — " +
                "ConnId={ConnId} Path={Path} Remote={Remote} Issues={Issues} Protocol={Protocol}",
                connectionId,
                path,
                remoteIp,
                string.Join(", ", pre.Issues),
                protocol
            );
        }
        else
        {
            _logger.LogDebug(
                "[CONN_PRE] ConnId={ConnId} Path={Path} Protocol={Protocol} State=OK",
                connectionId, path, protocol
            );
        }
        
        context.Response.OnCompleted(() =>
        {
            _logger.LogDebug(
                "[RESP_COMPLETED] ConnId={ConnId} Path={Path} Status={Status} " +
                "RequestAborted={Aborted}",
                connectionId,
                path,
                context.Response.StatusCode,
                context.RequestAborted.IsCancellationRequested);

            return Task.CompletedTask;
        });

        // seems to produce false positives?
        // Also register BEFORE calling next to catch abort during request
        // context.RequestAborted.Register(() =>
        // {
        //     _logger.LogWarning(
        //         "[REQ_ABORTED_DURING] ConnId={ConnId} Path={Path} " +
        //         "AbortedAt={Time}",
        //         connectionId,
        //         path,
        //         DateTimeOffset.UtcNow);
        // });

        Exception? requestException = null;
        var sw = Stopwatch.StartNew();

        try
        {
            // Log exact request headers coming FROM Traefik
            _logger.LogDebug(
                "[REQ_HEADERS] ConnId={ConnId} Path={Path} " +
                "Connection={Connection} KeepAlive={KeepAlive} " +
                "ContentLength={ContentLength} TransferEncoding={TE}",
                connectionId,
                path,
                context.Request.Headers.Connection.ToString(),
                context.Request.Headers.KeepAlive.ToString(),
                context.Request.ContentLength,
                context.Request.Headers.TransferEncoding.ToString());
            
            await _next(context);
            
            // Log exact response headers going BACK to Traefik
            _logger.LogDebug(
                "[RESP_HEADERS] ConnId={ConnId} Path={Path} Status={Status} " +
                "Connection={Connection} ContentLength={ContentLength} " +
                "TransferEncoding={TE}",
                connectionId,
                path,
                context.Response.StatusCode,
                context.Response.Headers.Connection.ToString(),
                context.Response.ContentLength,
                context.Response.Headers.TransferEncoding.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request exception: {Message}", ex.Message);
            requestException = ex;
            throw;
        }
        finally
        {
            sw.Stop();

            // --- POST-REQUEST CHECK ---
            var post = _inspector.Inspect(connectionId);

            var logLevel = (!post.IsHealthy || requestException != null)
                ? LogLevel.Error
                : LogLevel.Debug;

            _logger.Log(logLevel,
                "[CONN_POST] ConnId={ConnId} Path={Path} " +
                "Duration={Duration}ms Status={Status} " +
                "PreState={PreState} PostState={PostState} " +
                "LockHeld={LockHeld} TransportNull={TransportNull} " +
                "StillInTable={StillInTable} Issues={Issues} " +
                "Exception={Exception}",
                connectionId,
                context.Request.Path,
                sw.ElapsedMilliseconds,
                context.Response.StatusCode,
                pre.IsHealthy ? "OK" : "UNHEALTHY",
                post.IsHealthy ? "OK" : "UNHEALTHY",
                post.LockHeld,
                post.TransportNull,
                post.StillInTable,
                string.Join(", ", post.Issues),
                requestException?.Message);

            // Flag the specific case that causes your heartbeat crash:
            // connection is DONE with request but still in table with null lock
            if (!post.StillInTable && pre.IsHealthy)
            {
                _logger.LogDebug(
                    "[CONN_POST] ConnId={ConnId} cleanly removed from table",
                    connectionId);
            }
            else if (post.StillInTable && post.TransportNull)
            {
                _logger.LogError(
                    "[CONN_ZOMBIE] ConnId={ConnId} still in connection table " +
                    "but transport is null — WILL crash next heartbeat tick",
                    connectionId);
            }
            else if (post.StillInTable && post.LockHeld)
            {
                _logger.LogError(
                    "[CONN_STUCK] ConnId={ConnId} lock still held after request " +
                    "completed — potential deadlock",
                    connectionId);
            }
        }
    }
}