namespace Ingress.Configuration;

/// <summary>
/// Describes the response returned for a successful (non-handshake) delivery,
/// unless the handler overrides it. The <see cref="Body"/> supports token
/// substitution handled by <c>ResponseBuilder</c>.
/// </summary>
public sealed class ResponseConfig
{
    /// <summary>HTTP status code to return on success. Defaults to 200.</summary>
    public int Status { get; set; } = 200;

    /// <summary>Status code returned when validation fails. Defaults to 401.</summary>
    public int FailureStatus { get; set; } = 401;

    /// <summary>Response content type. Defaults to <c>text/plain</c>.</summary>
    public string ContentType { get; set; } = "text/plain";

    /// <summary>
    /// Response body template. Supported tokens:
    /// <c>{{uuid}}</c>, <c>{{name}}</c>, <c>{{json:path.to.field}}</c>.
    /// </summary>
    public string Body { get; set; } = "OK";
}
