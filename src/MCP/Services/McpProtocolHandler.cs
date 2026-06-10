using McpServer.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace McpServer.Services;

/// <summary>
/// Handles MCP protocol messages and routes them to appropriate services
/// </summary>
public class McpProtocolHandler : IMcpProtocolHandler
{
    private readonly ILogger<McpProtocolHandler> _logger;
    private readonly IToolMetadataService _toolMetadataService;
    private readonly IToolExecutionService _toolExecutionService;
    private readonly IResourceMetadataService? _resourceMetadataService;
    private readonly IResourceReadService? _resourceReadService;
    private readonly bool _logToolIO;

    private static readonly JsonSerializerOptions LogJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Match the wire format: enums logged as their string member names.
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };

    public McpProtocolHandler(
        ILogger<McpProtocolHandler> logger,
        IToolMetadataService toolMetadataService,
        IToolExecutionService toolExecutionService,
        IConfiguration configuration,
        IResourceMetadataService? resourceMetadataService = null,
        IResourceReadService? resourceReadService = null)
    {
        _logger = logger;
        _toolMetadataService = toolMetadataService;
        _toolExecutionService = toolExecutionService;
        _resourceMetadataService = resourceMetadataService;
        _resourceReadService = resourceReadService;
        _logToolIO = configuration.GetValue("Mcp:LogToolIO", false);
    }

    public async Task<McpResponse> HandleRequestAsync(
        IEntityContext? context,
        McpRequest request)
    {
        _logger.LogInformation("Handling MCP request: {Method}", request.Method);

        try
        {
            return request.Method switch
            {
                "initialize" => await HandleInitializeAsync(request),
                "tools/list" => await HandleToolsListAsync(request),
                "tools/call" => await HandleToolsCallAsync(context, request),
                "resources/list" => await HandleResourcesListAsync(request),
                "resources/read" => await HandleResourcesReadAsync(context, request),
                "ping" => HandlePing(request),
                _ => CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP request: {Method}", request.Method);
            return CreateErrorResponse(request.Id, -32603, $"Internal error: {ex.Message}");
        }
    }

    private async Task<McpResponse> HandleInitializeAsync(McpRequest request)
    {
        _logger.LogInformation("Initializing MCP connection");

        var result = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { },
                resources = new { listChanged = true },
                logging = new { }
            },
            serverInfo = new
            {
                name = "MCP SSE Server",
                version = "1.0.0"
            }
        };

        return new McpResponse
        {
            Jsonrpc = "2.0",
            Result = result,
            Id = request.Id
        };
    }

    private async Task<McpResponse> HandleToolsListAsync(McpRequest request)
    {
        _logger.LogInformation("Listing available tools");

        // Get tools dynamically on every request. ToolMetadata serializes itself with
        // the right shape (camelCase via [JsonPropertyName], outputSchema and nested
        // PropertySchema fields conditionally written), so emit it directly rather
        // than re-projecting and dropping fields.
        var tools = await _toolMetadataService.GetAvailableToolsAsync();

        return new McpResponse
        {
            Jsonrpc = "2.0",
            Result = new { tools },
            Id = request.Id
        };
    }

    private async Task<McpResponse> HandleToolsCallAsync(
        IEntityContext? context,
        McpRequest request)
    {
        if (!_logToolIO)
            return await HandleToolsCallCoreAsync(context, request);

        LogToolCallRequest(request);
        var response = await HandleToolsCallCoreAsync(context, request);
        LogToolCallResponse(request, response);
        return response;
    }

    private async Task<McpResponse> HandleToolsCallCoreAsync(
        IEntityContext? context,
        McpRequest request)
    {
        if (request.Params == null)
        {
            return CreateErrorResponse(request.Id, -32602, "Invalid params: params is required");
        }

        ToolCallRequest? toolCall;
        try
        {
            var paramsJson = JsonSerializer.Serialize(request.Params);
            toolCall = JsonSerializer.Deserialize<ToolCallRequest>(paramsJson);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(request.Id, -32602, $"Invalid params: {ex.Message}");
        }

        if (toolCall == null || string.IsNullOrEmpty(toolCall.Name))
        {
            return CreateErrorResponse(request.Id, -32602, "Invalid params: tool name is required");
        }

        _logger.LogInformation("Calling tool: {ToolName}", toolCall.Name);

        // Check if tool requires authentication
        var toolMetadata = await _toolMetadataService.GetToolMetadataAsync(toolCall.Name);
        if (toolMetadata == null)
        {
            return CreateErrorResponse(request.Id, -32602, $"Tool not found: {toolCall.Name}");
        }

        bool requiresAuthChallenge = false;
        ToolCallResult result;
        if (toolMetadata.RequiresAuthentication && context == null)
        {
            // Tool-level error so the LLM sees the message; the controller flag
            // additionally triggers a JwtBearer challenge for the OAuth refresh path.
            result = ToolCallResult.Error($"Authentication required for tool '{toolCall.Name}'.");
            requiresAuthChallenge = true;
        }
        else
        {
            result = await _toolExecutionService.ExecuteToolAsync(context, toolCall.Name, toolCall.Arguments);
        }

        // structuredContent is part of the standard tools/call result shape (MCP
        // 2025-06-18+); emit whenever the tool produced one. The legacy content[]
        // text fallback is always dual-emitted for older clients — ToolCallResult.Structured
        // already populates both fields.
        var payload = new Dictionary<string, object?>
        {
            ["content"] = result.Content.Select(c => new { type = c.Type, text = c.Text }).ToList()
        };
        if (result.StructuredContent != null) payload["structuredContent"] = result.StructuredContent;
        if (result.IsError) payload["isError"] = true;

        return new McpResponse
        {
            Jsonrpc = "2.0",
            Result = payload,
            Id = request.Id,
            TriggersListChanged = result.TriggersListChanged,
            RequiresAuthChallenge = requiresAuthChallenge
        };
    }

    private async Task<McpResponse> HandleResourcesListAsync(McpRequest request)
    {
        if (_resourceMetadataService == null)
        {
            return CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}");
        }

        _logger.LogInformation("Listing available resources");

        // Mirror tools/list: return all resources regardless of context. Auth is
        // enforced at read time via metadata.RequiresAuthentication.
        var resources = await _resourceMetadataService.GetAvailableResourcesAsync();

        return new McpResponse
        {
            Jsonrpc = "2.0",
            Result = new { resources },
            Id = request.Id
        };
    }

    private async Task<McpResponse> HandleResourcesReadAsync(
        IEntityContext? context,
        McpRequest request)
    {
        if (_resourceReadService == null || _resourceMetadataService == null)
        {
            return CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}");
        }

        if (request.Params == null)
        {
            return CreateErrorResponse(request.Id, -32602, "Invalid params: params is required");
        }

        ResourcesReadRequest? readRequest;
        try
        {
            var paramsJson = JsonSerializer.Serialize(request.Params);
            readRequest = JsonSerializer.Deserialize<ResourcesReadRequest>(paramsJson);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(request.Id, -32602, $"Invalid params: {ex.Message}");
        }

        if (readRequest == null || string.IsNullOrEmpty(readRequest.Uri))
        {
            return CreateErrorResponse(request.Id, -32602, "Invalid params: uri is required");
        }

        _logger.LogInformation("Reading resource: {Uri}", readRequest.Uri);

        var metadata = await _resourceMetadataService.GetResourceMetadataAsync(readRequest.Uri);
        if (metadata == null)
        {
            return CreateErrorResponse(request.Id, -32602, $"Resource not found: {readRequest.Uri}");
        }

        bool requiresAuthChallenge = false;
        ResourceReadResult result;
        if (metadata.RequiresAuthentication && context == null)
        {
            // In-band error so the LLM sees the message; flag triggers a JwtBearer
            // challenge so MCP clients receive WWW-Authenticate and refresh tokens.
            result = ResourceReadResult.Error($"Authentication required for resource '{readRequest.Uri}'.");
            requiresAuthChallenge = true;
        }
        else
        {
            result = await _resourceReadService.ReadResourceAsync(context, readRequest.Uri);
        }

        var payload = new Dictionary<string, object?>
        {
            ["contents"] = result.Contents
        };
        if (result.IsError) payload["isError"] = true;

        return new McpResponse
        {
            Jsonrpc = "2.0",
            Result = payload,
            Id = request.Id,
            RequiresAuthChallenge = requiresAuthChallenge
        };
    }

    private McpResponse HandlePing(McpRequest request)
    {
        return new McpResponse
        {
            Jsonrpc = "2.0",
            Result = new { }, // MCP spec requires empty object for ping response
            Id = request.Id
        };
    }

    private McpResponse CreateErrorResponse(object? id, int code, string message)
    {
        return new McpResponse
        {
            Jsonrpc = "2.0",
            Error = new McpError
            {
                Code = code,
                Message = message
            },
            Id = id
        };
    }

    private void LogToolCallRequest(McpRequest request)
    {
        var body = SafeSerialize(request.Params);
        _logger.LogInformation(
            "── MCP tools/call ▶ id={RequestId}\n{Params}",
            request.Id, body);
    }

    private void LogToolCallResponse(McpRequest request, McpResponse response)
    {
        var kind = response.Error != null ? "ERROR" : "OK";
        var body = SafeSerialize((object?)response.Error ?? response.Result);
        _logger.LogInformation(
            "── MCP tools/call ◀ id={RequestId} {Kind}\n{Body}",
            request.Id, kind, body);
    }

    private static string SafeSerialize(object? value)
    {
        if (value == null) return "(none)";
        try
        {
            return JsonSerializer.Serialize(value, LogJsonOptions);
        }
        catch (Exception ex)
        {
            return $"(serialization error: {ex.Message})";
        }
    }
}