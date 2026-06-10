namespace N8n.Controllers;

/// <summary>
/// An n8n <c>loadOptions</c> entry. n8n expects <c>name</c> (display) and <c>value</c>
/// (stored) — to match <c>INodePropertyOptions</c>.
/// </summary>
public sealed record N8nOption(string Name, string Value, string Description);

/// <summary>Body the n8n trigger node POSTs to register its webhook (<c>create</c>).</summary>
public sealed record SubscribeRequest(string Object, string Event, string TargetUrl);

/// <summary>Response to a subscribe call; the node stores <c>Id</c> to delete later.</summary>
public sealed record SubscribeResponse(string Id, string Object, string Event, string TargetUrl);

/// <summary>Answer to the node's <c>checkExists</c> probe.</summary>
public sealed record ExistsResponse(bool Exists, string Id);
