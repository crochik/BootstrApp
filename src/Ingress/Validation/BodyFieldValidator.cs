using System.Text.Json;
using Ingress.Configuration;
using Ingress.Engine;

namespace Ingress.Validation;

/// <summary>
/// Validates that a value inside the JSON body equals an expected value. A
/// <c>[]</c> segment in the path iterates an array and requires every element to
/// match — e.g. <c>value[].clientState</c> validates the shared secret carried in
/// each Microsoft Graph change notification.
/// </summary>
public sealed class BodyFieldValidator : IWebhookValidator
{
    public string Type => "bodyField";

    public ValidationResult Validate(WebhookContext context, AuthConfig config)
    {
        if (string.IsNullOrEmpty(config.Path))
        {
            return ValidationResult.Fail("bodyField: no path configured");
        }

        if (context.RawBody.Length == 0)
        {
            return ValidationResult.Fail("bodyField: empty body");
        }

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(context.RawBody);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ValidationResult.Fail("bodyField: invalid JSON body");
        }

        var matched = Matches(root, config.Path.Split('.', StringSplitOptions.RemoveEmptyEntries), 0, config.Value ?? string.Empty);
        return matched ? ValidationResult.Ok() : ValidationResult.Fail($"bodyField: '{config.Path}' did not match");
    }

    private static bool Matches(JsonElement element, string[] segments, int index, string expected)
    {
        if (index == segments.Length)
        {
            return element.ValueKind == JsonValueKind.String && element.GetString() == expected;
        }

        var segment = segments[index];
        if (segment.EndsWith("[]", StringComparison.Ordinal))
        {
            var name = segment[..^2];
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(name, out var array) ||
                array.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var any = false;
            foreach (var item in array.EnumerateArray())
            {
                any = true;
                if (!Matches(item, segments, index + 1, expected))
                {
                    return false; // Every element must match.
                }
            }

            return any;
        }

        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out var next))
        {
            return false;
        }

        return Matches(next, segments, index + 1, expected);
    }
}
