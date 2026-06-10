using DocuSeal.Api.Models;

namespace DocuSeal.Api.Clients;

/// <summary>
/// Client for Templates operations
/// </summary>
public interface ITemplates
{

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
    Task<GetTemplatesResponse> GetTemplatesAsync(string? q, string? slug, string? externalId, string? folder, bool? archived, int? limit, int? after, int? before, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a template
    /// </summary>
    /// <param name="id">The unique identifier of the document template.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<GetTemplateResponse> GetTemplateAsync(int id, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive a template
    /// </summary>
    /// <param name="id">The unique identifier of the document template.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ArchiveTemplateResponse> ArchiveTemplateAsync(int id, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a template
    /// </summary>
    /// <param name="id">The unique identifier of the document template.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<UpdateTemplateResponse> UpdateTemplateAsync(int id, UpdateTemplateRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update template documents
    /// </summary>
    /// <param name="id">The unique identifier of the documents template.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AddDocumentToTemplateResponse> AddDocumentToTemplateAsync(int id, AddDocumentToTemplateRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clone a template
    /// </summary>
    /// <param name="id">The unique identifier of the documents template.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<CloneTemplateResponse> CloneTemplateAsync(int id, CloneTemplateRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a template from HTML
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<CreateTemplateFromHtmlResponse> CreateTemplateFromHtmlAsync(CreateTemplateFromHtmlRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a template from Word DOCX
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<CreateTemplateFromDocxResponse> CreateTemplateFromDocxAsync(CreateTemplateFromDocxRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a template from PDF
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<CreateTemplateFromPdfResponse> CreateTemplateFromPdfAsync(CreateTemplateFromPdfRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge templates
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<MergeTemplateResponse> MergeTemplateAsync(MergeTemplateRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);
}
