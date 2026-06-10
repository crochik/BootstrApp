namespace Webhook.Zapier.Controllers;

/// <summary>An object as shown in a Zapier dynamic dropdown.</summary>
public sealed record ObjectDto(string Key, string Label, string Description);

/// <summary>An event as shown in a Zapier dynamic dropdown.</summary>
public sealed record EventDto(string Key, string Label, string Description);

/// <summary>A flattened object+event pair, handy for a single combined trigger dropdown.</summary>
public sealed record TriggerDto(
    string Key,
    string Label,
    string ObjectKey,
    string ObjectLabel,
    string EventKey,
    string EventLabel,
    string Description);

/// <summary>Body Zapier POSTs to subscribe (REST Hook). <c>TargetUrl</c> is Zapier's callback.</summary>
public sealed record SubscribeRequest(string? Object, string? Event, string? TargetUrl);

/// <summary>Response to a subscribe call; Zapier stores <c>Id</c> to unsubscribe later.</summary>
public sealed record SubscribeResponse(string Id, string Object, string Event, string TargetUrl);

/// <summary>Body for the demo emit endpoint.</summary>
public sealed record EmitRequest(string? Object, string? Event);
