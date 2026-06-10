using System.Text;
using System.Text.Json;
using Ingress.Engine;

namespace Ingress.Responses;

/// <summary>
/// Final response produced for a delivery: status code, content type and body.
/// </summary>
public readonly record struct BuiltResponse(int Status, string ContentType, string Body);

/// <summary>
/// Builds the response for a successful delivery by combining the webhook's
/// <c>ResponseConfig</c> with any overrides returned by the handler. Body
/// templates support the tokens <c>{{uuid}}</c>, <c>{{name}}</c> and
/// <c>{{json:path.to.field}}</c> (dot-path lookup into the JSON request body).
/// </summary>
public static class ResponseBuilder
{
    public static BuiltResponse Build(WebhookContext context, WebhookResult result)
    {
        var config = context.Definition.Response;

        var status = result.StatusOverride ?? config.Status;
        var contentType = result.ContentTypeOverride ?? config.ContentType;
        var body = result.BodyOverride ?? Substitute(config.Body, context);

        return new BuiltResponse(status, contentType, body);
    }

    internal static string Substitute(string template, WebhookContext context)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains("{{", StringComparison.Ordinal))
        {
            return template;
        }

        var sb = new StringBuilder(template.Length);
        var index = 0;
        while (index < template.Length)
        {
            var open = template.IndexOf("{{", index, StringComparison.Ordinal);
            if (open < 0)
            {
                sb.Append(template, index, template.Length - index);
                break;
            }

            sb.Append(template, index, open - index);
            var close = template.IndexOf("}}", open, StringComparison.Ordinal);
            if (close < 0)
            {
                sb.Append(template, open, template.Length - open);
                break;
            }

            var token = template[(open + 2)..close].Trim();
            sb.Append(Resolve(token, context));
            index = close + 2;
        }

        return sb.ToString();
    }

    private static string Resolve(string token, WebhookContext context)
    {
        if (token.Equals("uuid", StringComparison.OrdinalIgnoreCase))
        {
            return context.Definition.Uuid;
        }

        if (token.Equals("name", StringComparison.OrdinalIgnoreCase))
        {
            return context.Definition.Name;
        }

        if (token.StartsWith("json:", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveJsonPath(token["json:".Length..], context);
        }

        return string.Empty;
    }

    private static string ResolveJsonPath(string path, WebhookContext context)
    {
        if (context.RawBody.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(context.RawBody);
            var element = doc.RootElement;
            foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    !element.TryGetProperty(segment, out element))
                {
                    return string.Empty;
                }
            }

            return element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? string.Empty
                : element.GetRawText();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
