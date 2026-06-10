namespace Services;

public class ConnectionState
{
    public string ConnectionId { get; init; } = "";
    public bool IsHealthy { get; set; }
    public bool StillInTable { get; set; }
    public bool LockNull { get; set; }
    public bool LockHeld { get; set; }
    public bool TransportNull { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsAborted { get; set; }
    public List<string> Issues { get; } = [];

    public static ConnectionState Unknown(string id) => new()
    {
        ConnectionId = id,
        IsHealthy = false,
        Issues = { "could not inspect — inspector not initialized" }
    };
    
    public static ConnectionState NullEntry(string id) => new()
    {
        ConnectionId = id,
        StillInTable = true,
        IsHealthy = false,
        Issues = { "connection entry in table is null" }
    };
    
    public static ConnectionState Unknown(string id, string reason) => new()
    {
        ConnectionId = id,
        IsHealthy = false,
        Issues = { reason }
    };
    
}