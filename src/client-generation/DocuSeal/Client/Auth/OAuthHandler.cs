using System.Net.Http.Headers;

namespace DocuSeal.Api.Auth;

/// <summary>
/// Delegating handler for OAuth 2.0 authentication
/// </summary>
public class OAuthHandler : DelegatingHandler
{
    private string? _accessToken;

    /// <summary>
    /// Sets the OAuth access token
    /// </summary>
    public void SetAccessToken(string accessToken)
    {
        _accessToken = accessToken;
    }

    /// <summary>
    /// Clears the OAuth access token
    /// </summary>
    public void ClearAccessToken()
    {
        _accessToken = null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
