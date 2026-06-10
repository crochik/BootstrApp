using System;
using System.Linq;
using PI.Shared.Integrations.Subscriptions;

namespace Webhooks.Controllers;

/// <summary>An object an application can subscribe to.</summary>
public sealed record ObjectDto(string Key, string Label, string Description);

/// <summary>An event an object can emit.</summary>
public sealed record EventDto(string Key, string Label, string Description);

/// <summary>A flattened object+event pair, the full set of subscribable event types.</summary>
public sealed record EventTypeDto(
    string Key,
    string ObjectKey,
    string ObjectLabel,
    string EventKey,
    string EventLabel,
    string Description);

/// <summary>Body to subscribe: deliver <c>Object</c>/<c>Event</c> to <c>TargetUrl</c>.</summary>
public sealed record SubscribeRequest(string Object, string Event, string TargetUrl);

/// <summary>
/// A subscription. <see cref="Secret"/> is returned only on create and on get-by-id (so the
/// application can verify the HMAC signature) and is <c>null</c> in list responses.
/// </summary>
public sealed record SubscriptionDto(
    string Id,
    string Object,
    string Event,
    string TargetUrl,
    string SignatureHeader,
    string Secret,
    DateTime CreatedOn)
{
    public static SubscriptionDto From(IntegrationSubscription s, bool includeSecret) => new(
        s.Id.ToString(),
        s.ObjectType,
        s.Keys?.FirstOrDefault(),
        s.Url,
        s.SignatureHeader,
        includeSecret ? s.Secret : null,
        s.CreatedOn);
}
