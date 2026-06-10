using System.Net.Http.Json;
using System.Text.Json;
using DocuSeal.Api.Models;

namespace DocuSeal.Api.Clients;

/// <summary>
/// Client for Submissions operations
/// </summary>
public partial class Submissions : ISubmissions
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string BaseUrl = "https://api.docuseal.com";

    public Submissions(HttpClient httpClient, TimeSpan? timeout = null)
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
    /// List all submissions
    /// </summary>
    /// <param name="templateId">The template ID allows you to receive only the submissions created from that specific template.</param>
    /// <param name="status">Filter submissions by status.</param>
    /// <param name="q">Filter submissions based on submitters name, email or phone partial match.</param>
    /// <param name="slug">Filter submissions by unique slug.</param>
    /// <param name="templateFolder">Filter submissions by template folder name.</param>
    /// <param name="archived">Returns only archived submissions when `true` and only active submissions when `false`.</param>
    /// <param name="limit">The number of submissions to return. Default value is 10. Maximum value is 100.</param>
    /// <param name="after">The unique identifier of the submission to start the list from. It allows you to receive only submissions with an ID greater than the specified value. Pass ID value from the `pagination.next` response to load the next batch of submissions.</param>
    /// <param name="before">The unique identifier of the submission that marks the end of the list. It allows you to receive only submissions with an ID less than the specified value.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<GetSubmissionsResponse> GetSubmissionsAsync(int? templateId, string? status, string? q, string? slug, string? templateFolder, bool? archived, int? limit, int? after, int? before, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submissions";

        var queryParams = new List<string>();
        if (templateId != null)
        {
            queryParams.Add($"template_id={Uri.EscapeDataString(templateId.ToString()!)}");
        }
        if (status != null)
        {
            queryParams.Add($"status={Uri.EscapeDataString(status.ToString()!)}");
        }
        if (q != null)
        {
            queryParams.Add($"q={Uri.EscapeDataString(q.ToString()!)}");
        }
        if (slug != null)
        {
            queryParams.Add($"slug={Uri.EscapeDataString(slug.ToString()!)}");
        }
        if (templateFolder != null)
        {
            queryParams.Add($"template_folder={Uri.EscapeDataString(templateFolder.ToString()!)}");
        }
        if (archived != null)
        {
            queryParams.Add($"archived={Uri.EscapeDataString(archived.ToString()!)}");
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
        var _result = GetSubmissionsResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Create a submission
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<List<object>> CreateSubmissionAsync(CreateSubmissionRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submissions";


        var _request = new HttpRequestMessage(HttpMethod.Post, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = CreateSubmissionRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _result = await _response.Content.ReadFromJsonAsync<List<object>>(options: _jsonOptions, cancellationToken: cancellationToken);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Get a submission
    /// </summary>
    /// <param name="id">The unique identifier of the submission.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<GetSubmissionResponse> GetSubmissionAsync(int id, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submissions/{id}"
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
        var _result = GetSubmissionResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Archive a submission
    /// </summary>
    /// <param name="id">The unique identifier of the submission.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<ArchiveSubmissionResponse> ArchiveSubmissionAsync(int id, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submissions/{id}"
            .Replace("id", Uri.EscapeDataString(id.ToString()))
            ;


        var _request = new HttpRequestMessage(HttpMethod.Delete, path);


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
        var _result = ArchiveSubmissionResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Get submission documents
    /// </summary>
    /// <param name="id">The unique identifier of the submission.</param>
    /// <param name="merge">When `true`, merges all documents into a single PDF.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<GetSubmissionDocumentsResponse> GetSubmissionDocumentsAsync(int id, bool? merge = false, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submissions/{id}/documents"
            .Replace("id", Uri.EscapeDataString(id.ToString()))
            ;

        var queryParams = new List<string>();
        if (merge != null)
        {
            queryParams.Add($"merge={Uri.EscapeDataString(merge.ToString()!)}");
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
        var _result = GetSubmissionDocumentsResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Create submissions from emails
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<List<object>> CreateSubmissionsFromEmailsAsync(CreateSubmissionsFromEmailsRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submissions/emails";


        var _request = new HttpRequestMessage(HttpMethod.Post, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = CreateSubmissionsFromEmailsRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _result = await _response.Content.ReadFromJsonAsync<List<object>>(options: _jsonOptions, cancellationToken: cancellationToken);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Create a submission from PDF
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CreateSubmissionFromPdfResponse> CreateSubmissionFromPdfAsync(CreateSubmissionFromPdfRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submissions/pdf";


        var _request = new HttpRequestMessage(HttpMethod.Post, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = CreateSubmissionFromPdfRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = CreateSubmissionFromPdfResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Create a submission from DOCX
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CreateSubmissionFromDocxResponse> CreateSubmissionFromDocxAsync(CreateSubmissionFromDocxRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submissions/docx";


        var _request = new HttpRequestMessage(HttpMethod.Post, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = CreateSubmissionFromDocxRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = CreateSubmissionFromDocxResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Create a submission from HTML
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CreateSubmissionFromHtmlResponse> CreateSubmissionFromHtmlAsync(CreateSubmissionFromHtmlRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/submissions/html";


        var _request = new HttpRequestMessage(HttpMethod.Post, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = CreateSubmissionFromHtmlRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = CreateSubmissionFromHtmlResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }
}
