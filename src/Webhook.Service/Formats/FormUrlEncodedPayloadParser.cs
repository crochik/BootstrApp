using Microsoft.AspNetCore.WebUtilities;
using Webhook.Service.Engine;

namespace Webhook.Service.Formats;

/// <summary>Parses <c>application/x-www-form-urlencoded</c> bodies into a string dictionary.</summary>
public sealed class FormUrlEncodedPayloadParser : IPayloadParser
{
    public string Format => "form";

    public object? Parse(WebhookContext context)
    {
        if (context.RawBody.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        var parsed = QueryHelpers.ParseQuery(context.BodyText);
        return parsed.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
    }
}
