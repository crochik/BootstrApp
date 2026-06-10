using System.Net.Http.Headers;
using System.Text;

namespace ZipTax.Auth;

/// <summary>
/// Delegating handler for Basic authentication
/// </summary>
public class BasicAuthHandler : DelegatingHandler
{
    private string? _username;
    private string? _password;

    /// <summary>
    /// Sets the Basic authentication credentials
    /// </summary>
    public void SetCredentials(string username, string password)
    {
        _username = username;
        _password = password;
    }

    /// <summary>
    /// Clears the Basic authentication credentials
    /// </summary>
    public void ClearCredentials()
    {
        _username = null;
        _password = null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
