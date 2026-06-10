namespace LangChain.Services;

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class HttpClientLoggingHandler : DelegatingHandler
{
    private readonly ILogger<HttpClientLoggingHandler> _logger;

    public HttpClientLoggingHandler(ILogger<HttpClientLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Log the request
        var requestBody = default(string);
        _logger.LogInformation("➡️ Sending HTTP request: {Method} {Uri}", request.Method, request.RequestUri);
        if (request.Content != null)
        {
            requestBody = await request.Content.ReadAsStringAsync();
            // _logger.LogInformation("➡️ Request Body: {Body}", requestBody);
        }

        // Send the request
        var response = await base.SendAsync(request, cancellationToken);

        // Log the response
        _logger.LogInformation("⬅️ Received HTTP response: {StatusCode} from {Uri}", response.StatusCode, request.RequestUri);

        var responseBody = await response.Content.ReadAsStringAsync();
        // _logger.LogInformation("⬅️ Response Body: {Body}", responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini request failed: {Error}", responseBody);
        }

        return response;
    }
}