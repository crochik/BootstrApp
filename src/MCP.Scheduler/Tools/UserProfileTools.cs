using System.Text.Json;
using Crochik.Mongo;
using McpServer.Tools.Attributes;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace McpServer.Tools;

/// <summary>
/// Example tool class demonstrating the attribute-based tool registration pattern.
/// Add [McpTool] to any public method to expose it as an MCP tool.
/// Constructor injection is supported — add any registered services as constructor parameters.
/// </summary>
public class UserProfileTools(ILogger<UserProfileTools> logger, MongoConnection connection)
{
    [McpTool(Name = "get_user_profile", Description = "Get the current user's profile information",
        ExamplePrompts = new[] { "What is my profile info?", "Show me my name and email" })]
    public async Task<string> GetUserProfileAsync(IEntityContext context)
    {
        logger.LogInformation("GetUserProfile called");
        
        var user = await connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, context.UserId.Value)
            .FirstOrDefaultAsync();
        
        if (user == null)
        {
            return "Invalid session";
        }

        return 
$"""
Name: {user.Name}
Email: {user.Email}
Phone Number: {user.Phone}
""";
    }

    // [McpTool(Name = "update_user_profile", Description = "Update the current user's profile")]
    // public async Task<string> UpdateUserProfileAsync(
    //     IEntityContext context,
    //     [McpParameter(Description = "New email address", Required = false)] string? email = null,
    //     [McpParameter(Description = "New display name", Required = false)] string? displayName = null)
    // {
    //     logger.LogInformation("UpdateUserProfile called");
    //
    //     var updated = new Dictionary<string, string>();
    //     if (email != null) updated["email"] = email;
    //     if (displayName != null) updated["displayName"] = displayName;
    //
    //     await Task.CompletedTask;
    //     return JsonSerializer.Serialize(new
    //     {
    //         success = true,
    //         message = "Profile updated successfully",
    //         updatedFields = updated
    //     }, new JsonSerializerOptions { WriteIndented = true });
    // }
    //
    // [McpTool(Name = "list_documents", Description = "List all documents accessible to the user")]
    // public async Task<object> ListDocumentsAsync(
    //     IEntityContext context,
    //     [McpParameter(Description = "Maximum number of documents to return", Required = false)] int limit = 10,
    //     [McpParameter(Description = "Number of documents to skip", Required = false)] int offset = 0)
    // {
    //     logger.LogInformation("ListDocuments called: limit={Limit}, offset={Offset}", limit, offset);
    //
    //     var documents = Enumerable.Range(offset + 1, Math.Min(limit, 5)).Select(i => new
    //     {
    //         id = $"doc-{i}",
    //         title = $"Document {i}",
    //         createdAt = DateTime.UtcNow.AddDays(-i).ToString("o")
    //     }).ToList();
    //
    //     await Task.CompletedTask;
    //     return new { documents, total = 25, limit, offset };
    // }
    //
    // [McpTool(Name = "get_document", Description = "Get a specific document by ID")]
    // public async Task<object> GetDocumentAsync(
    //     IEntityContext context,
    //     [McpParameter(Description = "The ID of the document to retrieve")] string documentId)
    // {
    //     logger.LogInformation("GetDocument called: documentId={DocumentId}", documentId);
    //
    //     await Task.CompletedTask;
    //     return new
    //     {
    //         id = documentId,
    //         title = $"Document {documentId}",
    //         content = "This is the content of the document. In production, this would be fetched from a database.",
    //         createdAt = DateTime.UtcNow.AddDays(-7).ToString("o"),
    //         modifiedAt = DateTime.UtcNow.AddDays(-1).ToString("o")
    //     };
    // }
    //
    // [McpTool(Name = "calculate", Description = "Perform a mathematical calculation")]
    // public async Task<string> CalculateAsync(
    //     [McpParameter(Description = "Mathematical expression to evaluate (e.g., '2 + 2')")] string expression)
    // {
    //     logger.LogInformation("Calculate called: expression={Expression}", expression);
    //
    //     expression = expression.Replace(" ", "");
    //
    //     double result;
    //     if (expression.Contains('+'))
    //     {
    //         var parts = expression.Split('+');
    //         result = double.Parse(parts[0]) + double.Parse(parts[1]);
    //     }
    //     else if (expression.Contains('-') && !expression.StartsWith('-'))
    //     {
    //         var parts = expression.Split('-');
    //         result = double.Parse(parts[0]) - double.Parse(parts[1]);
    //     }
    //     else if (expression.Contains('*'))
    //     {
    //         var parts = expression.Split('*');
    //         result = double.Parse(parts[0]) * double.Parse(parts[1]);
    //     }
    //     else if (expression.Contains('/'))
    //     {
    //         var parts = expression.Split('/');
    //         result = double.Parse(parts[0]) / double.Parse(parts[1]);
    //     }
    //     else
    //     {
    //         result = double.Parse(expression);
    //     }
    //
    //     await Task.CompletedTask;
    //     return JsonSerializer.Serialize(new { expression, result });
    // }
}
