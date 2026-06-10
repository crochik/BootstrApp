using System.Net;
using System.Net.Sockets;
using Ingress.Configuration;
using Ingress.Engine;

namespace Ingress.Validation;

/// <summary>
/// Restricts delivery to a set of source IPs / CIDR ranges. When
/// <see cref="AuthConfig.TrustForwardedFor"/> is set, the left-most
/// <c>X-Forwarded-For</c> entry is used instead of the socket peer address.
/// </summary>
public sealed class IpAllowlistValidator : IWebhookValidator
{
    public string Type => "ipAllowlist";

    public ValidationResult Validate(WebhookContext context, AuthConfig config)
    {
        if (config.Ranges.Count == 0)
        {
            return ValidationResult.Fail("ipAllowlist: no ranges configured");
        }

        var candidate = ResolveClientIp(context, config);
        if (candidate is null || !IPAddress.TryParse(candidate, out var ip))
        {
            return ValidationResult.Fail("ipAllowlist: client IP unavailable");
        }

        foreach (var range in config.Ranges)
        {
            if (IsInRange(ip, range))
            {
                return ValidationResult.Ok();
            }
        }

        return ValidationResult.Fail($"ipAllowlist: {candidate} not in any allowed range");
    }

    private static string? ResolveClientIp(WebhookContext context, AuthConfig config)
    {
        if (config.TrustForwardedFor)
        {
            var forwarded = context.GetHeader("X-Forwarded-For");
            if (!string.IsNullOrEmpty(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }
        }

        return context.RemoteIp;
    }

    internal static bool IsInRange(IPAddress address, string range)
    {
        var slash = range.IndexOf('/');
        if (slash < 0)
        {
            // Plain address comparison.
            return IPAddress.TryParse(range, out var single) && single.Equals(Normalize(address));
        }

        if (!IPAddress.TryParse(range[..slash], out var network) ||
            !int.TryParse(range[(slash + 1)..], out var prefixLength))
        {
            return false;
        }

        address = Normalize(address);
        network = Normalize(network);
        if (address.AddressFamily != network.AddressFamily)
        {
            return false;
        }

        var addrBytes = address.GetAddressBytes();
        var netBytes = network.GetAddressBytes();
        if (prefixLength < 0 || prefixLength > addrBytes.Length * 8)
        {
            return false;
        }

        var fullBytes = prefixLength / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (addrBytes[i] != netBytes[i])
            {
                return false;
            }
        }

        var remainingBits = prefixLength % 8;
        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (addrBytes[fullBytes] & mask) == (netBytes[fullBytes] & mask);
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
