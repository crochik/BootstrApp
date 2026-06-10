using DocuSeal.Api.Models;

namespace DocuSeal.Api.Clients;

/// <summary>
/// Client for Submitters operations
/// </summary>
public interface ISubmitters
{

    /// <summary>
    /// Get a submitter
    /// </summary>
    /// <param name="id">The unique identifier of the submitter.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<GetSubmitterResponse> GetSubmitterAsync(int id, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a submitter
    /// </summary>
    /// <param name="id">The unique identifier of the submitter.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<UpdateSubmitterResponse> UpdateSubmitterAsync(int id, UpdateSubmitterRequest request, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);

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
    Task<GetSubmittersResponse> GetSubmittersAsync(int? submissionId, string? q, string? slug, DateTimeOffset? completedAfter, DateTimeOffset? completedBefore, string? externalId, int? limit, int? after, int? before, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);
}
