using DocuSeal.Api.Models;

namespace DocuSeal.Api.Clients;

/// <summary>
/// Client for Submissions operations
/// </summary>
public interface ISubmissions
{

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
    Task<GetSubmissionsResponse> GetSubmissionsAsync(int? templateId, string? status, string? q, string? slug, string? templateFolder, bool? archived, int? limit, int? after, int? before, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a submission
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<List<object>> CreateSubmissionAsync(CreateSubmissionRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a submission
    /// </summary>
    /// <param name="id">The unique identifier of the submission.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<GetSubmissionResponse> GetSubmissionAsync(int id, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive a submission
    /// </summary>
    /// <param name="id">The unique identifier of the submission.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ArchiveSubmissionResponse> ArchiveSubmissionAsync(int id, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get submission documents
    /// </summary>
    /// <param name="id">The unique identifier of the submission.</param>
    /// <param name="merge">When `true`, merges all documents into a single PDF.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<GetSubmissionDocumentsResponse> GetSubmissionDocumentsAsync(int id, bool? merge = false, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create submissions from emails
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<List<object>> CreateSubmissionsFromEmailsAsync(CreateSubmissionsFromEmailsRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a submission from PDF
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<CreateSubmissionFromPdfResponse> CreateSubmissionFromPdfAsync(CreateSubmissionFromPdfRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a submission from DOCX
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<CreateSubmissionFromDocxResponse> CreateSubmissionFromDocxAsync(CreateSubmissionFromDocxRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a submission from HTML
    /// </summary>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<CreateSubmissionFromHtmlResponse> CreateSubmissionFromHtmlAsync(CreateSubmissionFromHtmlRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);
}
