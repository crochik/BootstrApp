using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Hosting.Server;

namespace Services;

public class ConnectionStateInspector
{
    private readonly ILogger<ConnectionStateInspector> _logger;

    private object? _connectionManager;
    private FieldInfo? _connectionsField;
    private FieldInfo? _lockField;
    private FieldInfo? _transportField;
    private FieldInfo? _ctsField;
    private FieldInfo? _abortedField;
    private FieldInfo? _connectionReferenceField; // unwraps ConnectionReference -> KestrelConnection

    private bool _initialized;
    private readonly System.Threading.Lock _initLock = new();

    public ConnectionStateInspector(IServer server, ILogger<ConnectionStateInspector> logger)
    {
        _logger = logger;
        TryInitialize(server);
    }
    
    public ConnectionState Inspect(string connectionId)
    {
        if (!_initialized)
            return ConnectionState.Unknown(connectionId, "inspector not initialized");

        try
        {
            var connections = _connectionsField!.GetValue(_connectionManager)
                as IDictionary;

            if (connections == null)
                return ConnectionState.Unknown(connectionId, "could not read connection table");

            object? conn = null;
            foreach (DictionaryEntry entry in connections)
            {
                // Unwrap ConnectionReference -> actual connection object
                var reference = entry.Value;
                if (reference == null) continue;

                // Lazily resolve the inner connection field on ConnectionReference
                _connectionReferenceField ??= reference.GetType()
                    .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(f => f.FieldType.Name.Contains("KestrelConnection")
                                         || f.FieldType.Name.Contains("Connection"));

                if (_connectionReferenceField == null)
                {
                    // Dump ConnectionReference fields so we know what we're working with
                    _logger.LogWarning(
                        "Could not find inner connection field on ConnectionReference — " +
                        "available fields: {Fields}",
                        string.Join(", ", reference.GetType()
                            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                            .Select(f => $"{f.Name}:{f.FieldType.Name}")));
                    break;
                }

                var innerConn = _connectionReferenceField.GetValue(reference);
                if (innerConn == null) continue;

                var connIdProp = innerConn.GetType()
                    .GetProperty("ConnectionId", BindingFlags.Public | BindingFlags.Instance);

                if (connIdProp?.GetValue(innerConn) as string == connectionId)
                {
                    conn = innerConn;
                    break;
                }
            }

            if (conn == null)
                return new ConnectionState
                {
                    ConnectionId = connectionId,
                    StillInTable = false,
                    IsHealthy = true
                };

            return InspectConnection(connectionId, conn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inspect failed for {ConnId}", connectionId);
            return ConnectionState.Unknown(connectionId, $"inspection threw: {ex.Message}");
        }
    }

    private ConnectionState InspectConnection(string connectionId, object conn)
    {
        var connType = conn.GetType();
        var state = new ConnectionState { ConnectionId = connectionId, StillInTable = true };

        // Lazily resolve fields once against the real .NET 10 Kestrel type
        _lockField ??= ResolveField(connType, "_lock", "_connectionLock", "_shutdownLock");
        _transportField ??= ResolveField(connType, "_transport", "Transport", "_socketTransport");
        _ctsField ??= connType
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => f.FieldType == typeof(CancellationTokenSource));
        _abortedField ??= ResolveField(connType, "_aborted", "_connectionAborted");

        CheckLock(conn, state);
        CheckTransport(conn, state);
        CheckCancellation(conn, state);
        CheckAborted(conn, state);
        CheckZombieCondition(state);

        state.IsHealthy = state.Issues.Count == 0;
        return state;
    }

    private void CheckLock(object conn, ConnectionState state)
    {
        var lockVal = _lockField?.GetValue(conn);

        if (lockVal == null)
        {
            state.LockNull = true;
            state.Issues.Add("lock is null — heartbeat will NRE on next tick");
            return;
        }

        if (lockVal is not System.Threading.Lock netLock)
        {
            // Unexpected lock type — log what it actually is
            state.Issues.Add($"unexpected lock type: {lockVal.GetType().FullName}");
            return;
        }

        if (netLock.IsHeldByCurrentThread)
        {
            // Lock held by the thread doing inspection — reentrancy problem
            state.LockHeld = true;
            state.Issues.Add("lock held by current thread — reentrancy");
            return;
        }

        // Try to acquire with 0ms timeout — non-blocking
        // If we can't get it, another thread is holding it
        if (netLock.TryEnter(0))
        {
            netLock.Exit();
            state.LockHeld = false;
        }
        else
        {
            state.LockHeld = true;
            state.Issues.Add("lock held by another thread — contention at time of check");
        }
    }

    private void CheckTransport(object conn, ConnectionState state)
    {
        if (_transportField == null)
        {
            state.Issues.Add("transport field not found — run KestrelFieldDumper");
            return;
        }

        var transportVal = _transportField.GetValue(conn);
        state.TransportNull = transportVal == null;

        if (state.TransportNull)
            state.Issues.Add("transport is null");
    }

    private void CheckCancellation(object conn, ConnectionState state)
    {
        if (_ctsField == null) return;

        var cts = _ctsField.GetValue(conn) as CancellationTokenSource;
        state.IsCancelled = cts?.IsCancellationRequested ?? false;

        if (state.IsCancelled)
            state.Issues.Add("cancellation token is cancelled");
    }

    private void CheckAborted(object conn, ConnectionState state)
    {
        if (_abortedField == null) return;

        var aborted = _abortedField.GetValue(conn);

        state.IsAborted = aborted switch
        {
            bool b => b,
            int i => i != 0,
            _ => false
        };

        if (state.IsAborted)
            state.Issues.Add("connection is aborted");
    }

    private static void CheckZombieCondition(ConnectionState state)
    {
        // The exact condition that causes your heartbeat NRE:
        // connection is torn down but still sitting in the connection table
        if (state.StillInTable && state.LockNull)
            state.Issues.Add("ZOMBIE: null lock + still in table = imminent heartbeat crash");

        if (state.StillInTable && state.TransportNull && state.IsCancelled)
            state.Issues.Add("ZOMBIE: cancelled + null transport + still in table");

        if (state.StillInTable && state.IsAborted && state.TransportNull)
            state.Issues.Add("ZOMBIE: aborted + null transport + still in table");
    }

    private void TryInitialize(IServer server)
    {
        using (_initLock.EnterScope())
        {
            if (_initialized) return;

            try
            {
                var serviceContext = server.GetType()
                    .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(f =>
                    {
                        try
                        {
                            return f.GetValue(server);
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .FirstOrDefault(v => v?.GetType().Name == "ServiceContext");

                if (serviceContext == null)
                {
                    _logger.LogWarning(
                        "ConnectionStateInspector: could not find ServiceContext — " +
                        "available fields: {Fields}",
                        string.Join(", ", server.GetType()
                            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                            .Select(f => $"{f.Name}:{f.FieldType.Name}")));
                    return;
                }

                _connectionManager = serviceContext.GetType()
                    .GetProperty("ConnectionManager", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(serviceContext);

                if (_connectionManager == null)
                {
                    _logger.LogWarning(
                        "ConnectionStateInspector: could not find ConnectionManager — " +
                        "available properties: {Props}",
                        string.Join(", ", serviceContext.GetType()
                            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Select(p => p.Name)));
                    return;
                }

                _connectionsField = _connectionManager.GetType()
                    .GetField("_connectionReferences", BindingFlags.NonPublic | BindingFlags.Instance);

                if (_connectionsField == null)
                {
                    _logger.LogWarning(
                        "ConnectionStateInspector: could not find _connections — " +
                        "available fields: {Fields}",
                        string.Join(", ", _connectionManager.GetType()
                            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                            .Select(f => $"{f.Name}:{f.FieldType.Name}")));
                    return;
                }

                // Add this after finding _connectionsField in TryInitialize
                var dictType = _connectionsField.FieldType;
                _logger.LogInformation(
                    "ConnectionStateInspector: _connectionReferences type={Type} " +
                    "KeyType={Key} ValueType={Value}",
                    dictType.Name,
                    dictType.GenericTypeArguments.ElementAtOrDefault(0)?.Name ?? "unknown",
                    dictType.GenericTypeArguments.ElementAtOrDefault(1)?.Name ?? "unknown");

                _initialized = true;
                _logger.LogInformation(
                    "ConnectionStateInspector initialized — " +
                    "ConnectionManager={Manager} ConnectionsField={Field}",
                    _connectionManager.GetType().Name,
                    _connectionsField.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConnectionStateInspector initialization failed");
            }
        }
    }

    private static FieldInfo? ResolveField(Type type, params string[] names) =>
        names
            .Select(n => type.GetField(n, BindingFlags.NonPublic | BindingFlags.Instance))
            .FirstOrDefault(f => f != null);
}