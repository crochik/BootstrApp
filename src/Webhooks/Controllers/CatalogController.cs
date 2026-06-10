using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Integrations.Catalog;

namespace Webhooks.Controllers;

/// <summary>
/// Discovery: the objects and events an application can subscribe to, derived from the
/// account's real object types. Also exposes a connection/identity check.
/// </summary>
[Authorize("webhooks")]
[Route("/webhooks/v1")]
public class CatalogController : APIController
{
    private readonly IObjectCatalog _catalog;

    public CatalogController(IObjectCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Connection test — confirms the token and returns the caller's identity.</summary>
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = Context.UserId,
        account = Context.AccountId,
        organization = Context.OrganizationId,
        role = Context.Role.ToString(),
    });

    /// <summary>Lists every subscribable object.</summary>
    [HttpGet("objects")]
    public async Task<IActionResult> GetObjects()
    {
        var objects = await _catalog.GetObjectsAsync(Context);
        return Ok(objects.Select(o => new ObjectDto(o.Key, o.Label, o.Description)));
    }

    /// <summary>Lists the events one object can emit.</summary>
    [HttpGet("objects/{objectKey}/events")]
    public async Task<IActionResult> GetEvents(string objectKey)
    {
        var obj = await _catalog.GetObjectAsync(Context, objectKey);
        if (obj is null)
        {
            return NotFound(new { error = $"unknown object '{objectKey}'" });
        }

        return Ok(obj.Events.Select(e => new EventDto(e.Key, e.Label, e.Description)));
    }

    /// <summary>The full flattened set of subscribable event types (<c>object.event</c>).</summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEventTypes()
    {
        var objects = await _catalog.GetObjectsAsync(Context);
        var eventTypes = objects
            .SelectMany(o => o.Events.Select(e => new EventTypeDto(
                Key: $"{o.Key}.{e.Key}",
                ObjectKey: o.Key,
                ObjectLabel: o.Label,
                EventKey: e.Key,
                EventLabel: e.Label,
                Description: e.Description)));

        return Ok(eventTypes);
    }
}
