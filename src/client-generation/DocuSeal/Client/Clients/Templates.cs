using System.Net.Http.Json;
using System.Text.Json;
using DocuSeal.Api.Models;

namespace DocuSeal.Api.Clients;

/// <summary>
/// Client for Templates operations
/// </summary>
public partial class Templates : ITemplates
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string BaseUrl = "https://api.docuseal.com";

    public Templates(HttpClient httpClient, TimeSpan? timeout = null)
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
    /// List all templates
    /// </summary>
    /// <param name="q">Filter templates based on the name partial match.</param>
    /// <param name="slug">Filter templates by unique slug.</param>
    /// <param name="externalId">The unique applications-specific identifier provided for the template via API or Embedded template form builder. It allows you to receive only templates with your specified external id.</param>
    /// <param name="folder">Filter templates by folder name.</param>
    /// <param name="archived">Get only archived templates instead of active ones.</param>
    /// <param name="limit">The number of templates to return. Default value is 10. Maximum value is 100.</param>
    /// <param name="after">The unique identifier of the template to start the list from. It allows you to receive only templates with id greater than the specified value. Pass ID value from the `pagination.next` response to load the next batch of templates.</param>
    /// <param name="before">The unique identifier of the template to end the list with. It allows you to receive only templates with id less than the specified value.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<GetTemplatesResponse> GetTemplatesAsync(string? q, string? slug, string? externalId, string? folder, bool? archived, int? limit, int? after, int? before, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/templates";

        var queryParams = new List<string>();
        if (q != null)
        {
            queryParams.Add($"q={Uri.EscapeDataString(q.ToString()!)}");
        }
        if (slug != null)
        {
            queryParams.Add($"slug={Uri.EscapeDataString(slug.ToString()!)}");
        }
        if (externalId != null)
        {
            queryParams.Add($"external_id={Uri.EscapeDataString(externalId.ToString()!)}");
        }
        if (folder != null)
        {
            queryParams.Add($"folder={Uri.EscapeDataString(folder.ToString()!)}");
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
        var _result = GetTemplatesResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Get a template
    /// </summary>
    /// <param name="id">The unique identifier of the document template.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<GetTemplateResponse> GetTemplateAsync(int id, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/templates/{id}"
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
        var _result = GetTemplateResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Archive a template
    /// </summary>
    /// <param name="id">The unique identifier of the document template.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<ArchiveTemplateResponse> ArchiveTemplateAsync(int id, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/templates/{id}"
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
        var _result = ArchiveTemplateResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Update a template
    /// </summary>
    /// <param name="id">The unique identifier of the document template.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<UpdateTemplateResponse> UpdateTemplateAsync(int id, UpdateTemplateRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/templates/{id}"
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

        var _bodyJson = UpdateTemplateRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = UpdateTemplateResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Update template documents
    /// </summary>
    /// <param name="id">The unique identifier of the documents template.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<AddDocumentToTemplateResponse> AddDocumentToTemplateAsync(int id, AddDocumentToTemplateRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/templates/{id}/documents"
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

        var _bodyJson = AddDocumentToTemplateRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = AddDocumentToTemplateResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Clone a template
    /// </summary>
    /// <param name="id">The unique identifier of the documents template.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CloneTemplateResponse> CloneTemplateAsync(int id, CloneTemplateRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/templates/{id}/clone"
            .Replace("id", Uri.EscapeDataString(id.ToString()))
            ;


        var _request = new HttpRequestMessage(HttpMethod.Post, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = CloneTemplateRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = CloneTemplateResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Create a template from HTML
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CreateTemplateFromHtmlResponse> CreateTemplateFromHtmlAsync(CreateTemplateFromHtmlRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/templates/html";


        var _request = new HttpRequestMessage(HttpMethod.Post, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = CreateTemplateFromHtmlRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = CreateTemplateFromHtmlResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Create a template from Word DOCX
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CreateTemplateFromDocxResponse> CreateTemplateFromDocxAsync(CreateTemplateFromDocxRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/templates/docx";


        var _request = new HttpRequestMessage(HttpMethod.Post, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = CreateTemplateFromDocxRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = CreateTemplateFromDocxResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Create a template from PDF
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CreateTemplateFromPdfResponse> CreateTemplateFromPdfAsync(CreateTemplateFromPdfRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/templates/pdf";


        var _request = new HttpRequestMessage(HttpMethod.Post, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = CreateTemplateFromPdfRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = CreateTemplateFromPdfResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }

    /// <summary>
    /// Merge templates
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<MergeTemplateResponse> MergeTemplateAsync(MergeTemplateRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/templates/merge";


        var _request = new HttpRequestMessage(HttpMethod.Post, path);


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var _bodyJson = MergeTemplateRequestSerializer.SerializeToJson(request);
        _request.Content = new StringContent(_bodyJson, System.Text.Encoding.UTF8, "application/json");

        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = MergeTemplateResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }
}
