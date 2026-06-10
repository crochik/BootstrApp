using System.Net.Http.Json;
using System.Text.Json;
using DocuSeal.Api.Models;

namespace DocuSeal.Api.Clients;

/// <summary>
/// Client for Submitters operations
/// </summary>
public partial class Submitters : ISubmitters
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string BaseUrl = "https://api.docuseal.com";

    public Submitters(HttpClient httpClient, TimeSpan? timeout = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        if (string.IsNullOrEmpty(_httpClient.BaseAddress?.ToString()))
        {
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }

        if (timeout.HasValue)
        {
            _httpClient.Timeout = timeout.Value;
        }
    }


    /// <summary>
    /// Get a submitter
    /// </summary>
    /// <param name="id">The unique identifier of the submitter.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<GetSubmitterResponse> GetSubmitterAsync(int id, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submitters/{id}"
            .Replace("id", Uri.EscapeDataString(id.ToString()))
            ;


        var _request = new HttpRequestMessage(HttpMethod.Get, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }


        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = GetSubmitterResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Update a submitter
    /// </summary>
    /// <param name="id">The unique identifier of the submitter.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<UpdateSubmitterResponse> UpdateSubmitterAsync(int id, UpdateSubmitterRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submitters/{id}"
            .Replace("id", Uri.EscapeDataString(id.ToString()))
            ;


        var _request = new HttpRequestMessage(HttpMethod.Put, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = UpdateSubmitterRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = UpdateSubmitterResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// List all submitters
    /// </summary>
    /// <param name="submissionId">The submission ID allows you to receive only the submitters related to that specific submission.</param>
    /// <param name="q">Filter submitters on name, email or phone partial match.</param>
    /// <param name="slug">Filter submitters by unique slug.</param>
    /// <param name="completedAfter">The date and time string value to filter submitters that completed the submission after the specified date and time.</param>
    /// <param name="completedBefore">The date and time string value to filter submitters that completed the submission before the specified date and time.</param>
    /// <param name="externalId">The unique applications-specific identifier provided for a submitter when initializing a signature request. It allows you to receive only submitters with a specified external id.</param>
    /// <param name="limit">The number of submitters to return. Default value is 10. Maximum value is 100.</param>
    /// <param name="after">The unique identifier of the submitter to start the list from. It allows you to receive only submitters with id greater than the specified value. Pass ID value from the `pagination.next` response to load the next batch of submitters.</param>
    /// <param name="before">The unique identifier of the submitter to end the list with. It allows you to receive only submitters with id less than the specified value.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<GetSubmittersResponse> GetSubmittersAsync(int? submissionId, string? q, string? slug, DateTimeOffset? completedAfter, DateTimeOffset? completedBefore, string? externalId, int? limit, int? after, int? before, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submitters";

        var queryParams = new List<string>();
        if (submissionId != null)
        {
            queryParams.Add($"submission_id={Uri.EscapeDataString(submissionId.ToString()!)}");
        }
        if (q != null)
        {
            queryParams.Add($"q={Uri.EscapeDataString(q.ToString()!)}");
        }
        if (slug != null)
        {
            queryParams.Add($"slug={Uri.EscapeDataString(slug.ToString()!)}");
        }
        if (completedAfter != null)
        {
            queryParams.Add($"completed_after={Uri.EscapeDataString(completedAfter.ToString()!)}");
        }
        if (completedBefore != null)
        {
            queryParams.Add($"completed_before={Uri.EscapeDataString(completedBefore.ToString()!)}");
        }
        if (externalId != null)
        {
            queryParams.Add($"external_id={Uri.EscapeDataString(externalId.ToString()!)}");
        }
        if (limit != null)
        {
            queryParams.Add($"limit={Uri.EscapeDataString(limit.ToString()!)}");
        }
        if (after != null)
        {
            queryParams.Add($"after={Uri.EscapeDataString(after.ToString()!)}");
        }
        if (before != null)
        {
            queryParams.Add($"before={Uri.EscapeDataString(before.ToString()!)}");
        }

        if (queryParams.Any())
        {
            path += "?" + string.Join("&", queryParams);
        }

        var _request = new HttpRequestMessage(HttpMethod.Get, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }


        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = GetSubmittersResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }
}
