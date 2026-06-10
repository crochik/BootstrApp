using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Webhook.Service.Configuration;
using Webhook.Service.Engine;

namespace Webhook.Service.Validation;

/// <summary>
/// Validates Twilio's <c>X-Twilio-Signature</c>. Unlike a plain body HMAC, Twilio
/// signs the exact request URL with the alphabetically-sorted form parameters
/// appended (key immediately followed by value, no separators), using HMAC-SHA1
/// keyed by the account auth token, base64-encoded.
/// <para>
/// Set <see cref="AuthConfig.Url"/> to the public webhook URL Twilio was configured
/// with when the server sits behind a proxy/tunnel. Form bodies
/// (<c>application/x-www-form-urlencoded</c>) are supported; JSON deliveries (which
/// Twilio signs via a <c>bodySHA256</c> query parameter) are not yet handled.
/// </para>
/// </summary>
public sealed class TwilioSignatureValidator : IWebhookValidator
{
    public string Type => "twilio";

    public ValidationResult Validate(WebhookContext context, AuthConfig config)
    {
        if (string.IsNullOrEmpty(config.Token))
        {
            return ValidationResult.Fail("twilio: no auth token configured");
        }

        var headerName = string.IsNullOrEmpty(config.Header) ? "X-Twilio-Signature" : config.Header;
        var provided = context.GetHeader(headerName);
        if (string.IsNullOrEmpty(provided))
        {
            return ValidationResult.Fail($"twilio: missing signature header '{headerName}'");
        }

        var url = config.Url ?? context.RequestUrl;
        if (string.IsNullOrEmpty(url))
        {
            return ValidationResult.Fail("twilio: request URL unavailable");
        }

        // JSON deliveries: Twilio signs the URL alone (which carries a bodySHA256
        // query parameter) and the body integrity is checked separately.
        if (context.Query.TryGetValue("bodySHA256", out var bodyHash))
        {
            url = EnsureQueryParam(url, "bodySHA256", bodyHash);
            if (!BodyHashMatches(context, bodyHash))
            {
                return ValidationResult.Fail("twilio: bodySHA256 mismatch");
            }

            return CompareSignature(url, config.Token, provided);
        }

        // Form deliveries: URL + each sorted form parameter (key then value, no separators).
        var builder = new StringBuilder(url);
        foreach (var (key, value) in SortedFormParameters(context))
        {
            builder.Append(key).Append(value);
        }

        return CompareSignature(builder.ToString(), config.Token, provided);
    }

    private static ValidationResult CompareSignature(string signingString, string token, string provided)
    {
        byte[] hash;
        using (var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(token)))
        {
            hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingString));
        }

        var expected = Convert.ToBase64String(hash);
        var match = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));

        return match ? ValidationResult.Ok() : ValidationResult.Fail("twilio: signature mismatch");
    }

    private static bool BodyHashMatches(WebhookContext context, string expectedHex)
    {
        var actual = Convert.ToHexString(SHA256.HashData(context.RawBody)).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expectedHex.ToLowerInvariant()));
    }

    private static string EnsureQueryParam(string url, string key, string value)
    {
        if (url.Contains($"{key}=", StringComparison.Ordinal))
        {
            return url;
        }

        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}{key}={value}";
    }

    private static IEnumerable<KeyValuePair<string, string>> SortedFormParameters(WebhookContext context)
    {
        if (context.RawBody.Length == 0)
        {
            yield break;
        }

        var parsed = QueryHelpers.ParseQuery(context.BodyText);
        foreach (var key in parsed.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            yield return new KeyValuePair<string, string>(key, parsed[key].ToString());
        }
    }
}
