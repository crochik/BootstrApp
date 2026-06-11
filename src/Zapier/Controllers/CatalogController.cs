using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Integrations.Catalog;

namespace Zapier.Controllers;

/// <summary>
/// Feeds Zapier's dynamic dropdowns. These endpoints make the integration generic:
/// Zapier reads the available objects and events here — discovered from the account's
/// real object types — at the moment a user configures a Zap, so nothing about the
/// domain is baked into the Zapier app.
/// </summary>
[Authorize("zapier")]
[Route("/zapier/v1")]
public class CatalogController : APIController
{
    private readonly IObjectCatalog _catalog;

    public CatalogController(IObjectCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Lists every object — source for the "Object" dropdown.</summary>
    [HttpGet("objects")]
    public async Task<IActionResult> GetObjects()
    {
        var objects = await _catalog.GetObjectsAsync(Context);
        return Ok(objects.Select(o => new ObjectDto(o.Key, o.Label, o.Description)));
    }

    /// <summary>Lists events for one object — source for the dependent "Event" dropdown.</summary>
    [HttpGet("objects/{objectKey}/events")]
    public async Task<IActionResult> GetEvents(string objectKey)
    {
        var events = await _catalog.GetEventsAsync(Context, objectKey);
        return Ok(events.Select(e => new EventDto(e.Key, e.Label, e.Description)));
    }
}
