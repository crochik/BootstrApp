using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmittersResponseDataItemSubmissionEventsItem
{
    /// <summary>
    /// Unique identifier of the submission event.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// Unique identifier of the submitter that triggered the event.
    /// </summary>
    [JsonPropertyName("submitter_id")]
    public required int SubmitterId { get; set; }

    /// <summary>
    /// Event type.
    /// </summary>
    [JsonPropertyName("event_type")]
    public required string EventType { get; set; }

    /// <summary>
    /// Date and time when the event was triggered.
    /// </summary>
    [JsonPropertyName("event_timestamp")]
    public required string EventTimestamp { get; set; }

    /// <summary>
    /// Additional event details object.
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }

}
