using System.Text.Json;
using Ingress.Configuration;

namespace Ingress.Engine;

/// <summary>
/// Detects and answers provider registration / verification handshakes,
/// short-circuiting normal handler dispatch. Supports the two most common
/// patterns: challenge echoed from a query parameter (Meta/Facebook) and
/// challenge echoed from a JSON body field (Slack).
/// </summary>
public static class RegistrationHandshake
{
    /// <summary>
    /// Query-parameter challenge (e.g. Meta <c>hub.challenge</c>, Microsoft Graph
    /// <c>validationToken</c>). These are unauthenticated verification pings, so they
    /// are answered <b>before</b> the validation pipeline runs.
    /// </summary>
    public static WebhookResult? TryHandleQuery(WebhookContext context)
    {
        var reg = context.Definition.Registration;
        return string.Equals(reg.Mode, "challengeQuery", StringComparison.OrdinalIgnoreCase)
            ? HandleQuery(context, reg)
            : null;
    }

    /// <summary>
    /// Body challenge (e.g. Slack <c>url_verification</c>). These arrive signed, so
    /// they are answered <b>after</b> the validation pipeline has run.
    /// </summary>
    public static WebhookResult? TryHandleBody(WebhookContext context)
    {
        var reg = context.Definition.Registration;
        return string.Equals(reg.Mode, "challengeBody", StringComparison.OrdinalIgnoreCase)
            ? HandleBody(context, reg)
            : null;
    }

    private static WebhookResult? HandleQuery(WebhookContext context, RegistrationConfig reg)
    {
        if (!context.Query.TryGetValue(reg.ChallengeParam, out var challenge))
        {
            return null; // Not a handshake request.
        }

        if (!string.IsNullOrEmpty(reg.VerifyParam))
        {
            context.Query.TryGetValue(reg.VerifyParam, out var presented);
            if (!string.Equals(presented, reg.VerifyValue, StringComparison.Ordinal))
            {
                return WebhookResult.Custom(403, "verification token mismatch");
            }
        }

        return WebhookResult.Custom(200, challenge);
    }

    private static WebhookResult? HandleBody(WebhookContext context, RegistrationConfig reg)
    {
        if (context.RawBody.Length == 0)
        {
            return null;
        }

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(context.RawBody);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(reg.TriggerField, out var trigger) ||
            trigger.ValueKind != JsonValueKind.String ||
            !string.Equals(trigger.GetString(), reg.TriggerValue, StringComparison.Ordinal))
        {
            return null; // Not a handshake request.
        }

        var challenge = root.TryGetProperty(reg.ChallengeField, out var field) && field.ValueKind == JsonValueKind.String
            ? field.GetString() ?? string.Empty
            : string.Empty;

        return WebhookResult.Custom(200, challenge);
    }
}
