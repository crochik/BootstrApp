using McpServer.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace McpServer.Services;

/// <summary>
/// Generic tool execution service that handles all non-authentication tools.
/// In production, this would route to database-driven tool implementations.
/// </summary>
public class ToolExecutionService : IToolExecutionService
{
    private readonly ILogger<ToolExecutionService> _logger;
    private readonly IToolMetadataService _toolMetadataService;

    public ToolExecutionService(
        ILogger<ToolExecutionService> logger,
        IToolMetadataService toolMetadataService)
    {
        _logger = logger;
        _toolMetadataService = toolMetadataService;
    }

    public async Task<ToolCallResult> ExecuteToolAsync(IEntityContext? context, string toolName, Dictionary<string, object>? arguments)
    {
        _logger.LogInformation("Executing tool: {ToolName}", toolName);

        try
        {
            // Get tool metadata to check if it exists
            var toolMetadata = await _toolMetadataService.GetToolMetadataAsync(toolName);
            if (toolMetadata == null)
            {
                return CreateErrorResult($"Tool '{toolName}' not found");
            }

            // Check authentication for all tools (OAuth required)
            if (toolMetadata.RequiresAuthentication && context==null)
            {
                return CreateErrorResult("Authentication required.");
            }

            // Route to appropriate tool handler
            // In production, this would query a database to find the appropriate handler
            return toolName.ToLowerInvariant() switch
            {
                "get_user_profile" => await ExecuteGetUserProfileAsync(context!),
                // "update_user_profile" => await ExecuteUpdateUserProfileAsync(username!, arguments),
                // "list_documents" => await ExecuteListDocumentsAsync(username!, arguments),
                // "get_document" => await ExecuteGetDocumentAsync(username!, arguments),
                // "calculate" => await ExecuteCalculateAsync(arguments),
                _ => CreateErrorResult($"Tool '{toolName}' is not implemented yet")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool: {ToolName}", toolName);
            return CreateErrorResult($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<ToolCallResult> ExecuteGetUserProfileAsync(IEntityContext context)
    {
        await Task.Delay(10); // Simulate database query

        var profile = new
        {
            // username = context.
            // email = $"{username}@example.com",
            // displayName = username.ToUpper(),
            createdAt = DateTime.UtcNow.AddDays(-30).ToString("o"),
            lastLogin = DateTime.UtcNow.ToString("o")
        };

        return new ToolCallResult
        {
            IsError = false,
            Content =
            [
                // new ToolContent
                // {
                //     Type = "text",
                //     Text = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true })
                // }
                new ToolContent
                {
                    Type = "text",
                    Text = @"
# User
Name: Felipe Crochik
Email: felipe@crochik.com

# Organization
Crochik Software Solutions, LLC
                     
                    ",
                }
            ]
        };
    }

    private async Task<ToolCallResult> ExecuteUpdateUserProfileAsync(string username, Dictionary<string, object>? arguments)
    {
        await Task.CompletedTask;

        var updates = new Dictionary<string, string>();
        if (arguments?.TryGetValue("email", out var email) is true)
            updates["email"] = email.ToString() ?? "";
        if (arguments?.TryGetValue("displayName", out var displayName) is true)
            updates["displayName"] = displayName.ToString() ?? "";

        var result = new
        {
            success = true,
            message = "Profile updated successfully",
            updatedFields = updates
        };

        return new ToolCallResult
        {
            IsError = false,
            Content = new List<ToolContent>
            {
                new ToolContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<ToolCallResult> ExecuteListDocumentsAsync(string username, Dictionary<string, object>? arguments)
    {
        await Task.Delay(10); // Simulate database query

        var limit = 10;
        var offset = 0;

        if (arguments?.TryGetValue("limit", out var limitObj) is true)
            int.TryParse(limitObj.ToString(), out limit);
        if (arguments?.TryGetValue("offset", out var offsetObj) is true)
            int.TryParse(offsetObj.ToString(), out offset);

        var documents = Enumerable.Range(offset + 1, Math.Min(limit, 5)).Select(i => new
        {
            id = $"doc-{i}",
            title = $"Document {i}",
            createdAt = DateTime.UtcNow.AddDays(-i).ToString("o"),
            owner = username
        }).ToList();

        var result = new
        {
            documents,
            total = 25,
            limit,
            offset
        };

        return new ToolCallResult
        {
            IsError = false,
            Content = new List<ToolContent>
            {
                new ToolContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<ToolCallResult> ExecuteGetDocumentAsync(string username, Dictionary<string, object>? arguments)
    {
        await Task.Delay(10); // Simulate database query

        if (arguments?.ContainsKey("documentId") != true)
        {
            return CreateErrorResult("documentId is required");
        }

        var documentId = arguments["documentId"].ToString();

        var document = new
        {
            id = documentId,
            title = $"Document {documentId}",
            content = "This is the content of the document. In production, this would be fetched from a database.",
            owner = username,
            createdAt = DateTime.UtcNow.AddDays(-7).ToString("o"),
            modifiedAt = DateTime.UtcNow.AddDays(-1).ToString("o")
        };

        return new ToolCallResult
        {
            IsError = false,
            Content = new List<ToolContent>
            {
                new ToolContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<ToolCallResult> ExecuteCalculateAsync(Dictionary<string, object>? arguments)
    {
        await Task.Delay(5); // Simulate calculation

        if (arguments?.ContainsKey("expression") != true)
        {
            return CreateErrorResult("expression is required");
        }

        var expression = arguments["expression"].ToString() ?? "";

        // Simple calculator (in production, use a proper expression parser)
        try
        {
            var result = EvaluateSimpleExpression(expression);
            return new ToolCallResult
            {
                IsError = false,
                Content = new List<ToolContent>
                {
                    new ToolContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(new
                        {
                            expression,
                            result
                        })
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return CreateErrorResult($"Invalid expression: {ex.Message}");
        }
    }

    private double EvaluateSimpleExpression(string expression)
    {
        // Very basic calculator - in production use a proper expression parser
        expression = expression.Replace(" ", "");

        if (expression.Contains("+"))
        {
            var parts = expression.Split('+');
            return double.Parse(parts[0]) + double.Parse(parts[1]);
        }
        if (expression.Contains("-") && !expression.StartsWith("-"))
        {
            var parts = expression.Split('-');
            return double.Parse(parts[0]) - double.Parse(parts[1]);
        }
        if (expression.Contains("*"))
        {
            var parts = expression.Split('*');
            return double.Parse(parts[0]) * double.Parse(parts[1]);
        }
        if (expression.Contains("/"))
        {
            var parts = expression.Split('/');
            return double.Parse(parts[0]) / double.Parse(parts[1]);
        }

        return double.Parse(expression);
    }

    private ToolCallResult CreateErrorResult(string message)
    {
        return new ToolCallResult
        {
            IsError = true,
            Content = new List<ToolContent>
            {
                new ToolContent
                {
                    Type = "text",
                    Text = message
                }
            }
        };
    }
}
