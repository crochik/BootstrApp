using System.Collections.Concurrent;
using Microsoft.AspNetCore.Connections;
using Serilog;

namespace Services;

public class RawConnectionTracker(ConnectionDelegate next)
{
    private static readonly ConcurrentDictionary<string, RawConnectionEntry> _rawConnections = new();
    
    // Expose for cross-referencing with your middleware
    public static IReadOnlyDictionary<string, RawConnectionEntry> ActiveRawConnections 
        => _rawConnections;

    public async Task OnConnectAsync(ConnectionContext context)
    {
        var connId = context.ConnectionId;
        var entry = new RawConnectionEntry
        {
            ConnId = connId,
            OpenedAt = DateTimeOffset.UtcNow,
            RemoteEndPoint = context.RemoteEndPoint?.ToString() ?? "unknown",
            LocalEndPoint = context.LocalEndPoint?.ToString() ?? "unknown",
        };

        _rawConnections[connId] = entry;

        // Log every raw TCP connection — this will show connections
        // that never reach your HTTP middleware
        Log.Information(
            "[RAW_CONN_OPEN] ConnId={ConnId} Remote={Remote} Local={Local}",
            connId,
            entry.RemoteEndPoint,
            entry.LocalEndPoint);

        try
        {
            await next(context);
            entry.ClosedCleanly = true;
        }
        catch (Exception ex)
        {
            entry.Exception = ex.Message;
            Log.Error(
                ex,
                "[RAW_CONN_ERROR] ConnId={ConnId} Remote={Remote} Error={Error}",
                connId,
                entry.RemoteEndPoint,
                ex.Message);
            throw;
        }
        finally
        {
            var duration = DateTimeOffset.UtcNow - entry.OpenedAt;
            entry.ClosedAt = DateTimeOffset.UtcNow;
            _rawConnections.TryRemove(connId, out _);

            // This is the key log — if ConnId appears here but NEVER in 
            // your CONN_PRE middleware logs, it never sent an HTTP request
            Log.Information(
                "[RAW_CONN_CLOSE] ConnId={ConnId} Remote={Remote} " +
                "Duration={Duration}ms HadHttpRequest={HadRequest} " +
                "ClosedCleanly={Clean} Exception={Ex}",
                connId,
                entry.RemoteEndPoint,
                duration.TotalMilliseconds,
                entry.HadHttpRequest,
                entry.ClosedCleanly,
                entry.Exception ?? "none");

            if (!entry.HadHttpRequest)
            {
                Log.Warning(
                    "[RAW_CONN_NO_HTTP] ConnId={ConnId} Remote={Remote} " +
                    "TCP connection opened but never sent an HTTP request — " +
                    "Duration={Duration}ms — likely Traefik probe, " +
                    "failed TLS handshake, or WebSocket",
                    connId,
                    entry.RemoteEndPoint,
                    duration.TotalMilliseconds);
            }
        }
    }

    // Called by your HTTP middleware to mark this connection as having had a request
    public static void MarkHadHttpRequest(string connId)
    {
        if (_rawConnections.TryGetValue(connId, out var entry))
            entry.HadHttpRequest = true;
    }
}