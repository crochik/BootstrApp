namespace McpServer.Services;

public interface IAuthenticationService
{
    bool ValidateToken(string token);
    string? GetUsernameFromToken(string token);
}
