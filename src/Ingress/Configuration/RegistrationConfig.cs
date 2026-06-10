namespace Ingress.Configuration;

/// <summary>
/// Describes the registration / verification handshake a provider performs when
/// a webhook URL is first registered. When a request matches the handshake it is
/// answered directly, short-circuiting normal handler dispatch.
/// </summary>
public sealed class RegistrationConfig
{
    /// <summary>
    /// Handshake mode:
    /// <list type="bullet">
    /// <item><c>none</c> – no special handshake (default).</item>
    /// <item><c>challengeQuery</c> – Meta/Facebook style: a GET whose
    /// <see cref="ChallengeParam"/> query value is echoed back verbatim, optionally
    /// gated by <see cref="VerifyParam"/>/<see cref="VerifyValue"/>.</item>
    /// <item><c>challengeBody</c> – Slack style: a JSON body whose <see cref="ChallengeField"/>
    /// is echoed back when <see cref="TriggerField"/> equals <see cref="TriggerValue"/>.</item>
    /// </list>
    /// </summary>
    public string Mode { get; set; } = "none";

    // --- challengeQuery ---

    /// <summary>Query parameter holding the challenge to echo (e.g. <c>hub.challenge</c>).</summary>
    public string ChallengeParam { get; set; } = "hub.challenge";

    /// <summary>Optional verify-token query parameter (e.g. <c>hub.verify_token</c>).</summary>
    public string? VerifyParam { get; set; }

    /// <summary>Expected value of <see cref="VerifyParam"/>; the handshake fails (403) if it differs.</summary>
    public string? VerifyValue { get; set; }

    // --- challengeBody ---

    /// <summary>JSON field whose value is echoed back (e.g. <c>challenge</c>).</summary>
    public string ChallengeField { get; set; } = "challenge";

    /// <summary>JSON field inspected to detect a handshake request (e.g. <c>type</c>).</summary>
    public string TriggerField { get; set; } = "type";

    /// <summary>Value of <see cref="TriggerField"/> that marks a handshake (e.g. <c>url_verification</c>).</summary>
    public string TriggerValue { get; set; } = "url_verification";
}
