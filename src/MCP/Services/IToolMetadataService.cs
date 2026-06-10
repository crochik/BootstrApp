using McpServer.Models;

namespace McpServer.Services;

public interface IToolMetadataService
{
    Task<List<ToolMetadata>> GetAvailableToolsAsync();
    Task<ToolMetadata?> GetToolMetadataAsync(string toolName);
}
