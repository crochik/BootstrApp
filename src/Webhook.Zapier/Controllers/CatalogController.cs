using Microsoft.AspNetCore.Mvc;
using Webhook.Integrations.Core.Catalog;

namespace Webhook.Zapier.Controllers;

/// <summary>
/// Feeds Zapier's dynamic dropdowns. These endpoints are what make the integration
/// generic: Zapier reads the available objects and events here, at the moment a user
/// configures a Zap, so nothing about the domain is baked into the Zapier app.
/// </summary>
[ApiController]
[Route("zapier")]
public sealed class CatalogController : ControllerBase
{
    private readonly IEventCatalog _catalog;

    public CatalogController(IEventCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Lists every object — source for the "Object" dropdown.</summary>
    [HttpGet("objects")]
    public IActionResult GetObjects() =>
        Ok(_catalog.GetObjects().Select(o => new ObjectDto(o.Key, o.Label, o.Description)));

    /// <summary>Lists events for one object — source for the dependent "Event" dropdown.</summary>
    [HttpGet("objects/{objectKey}/events")]
    public IActionResult GetEvents(string objectKey)
    {
        if (!_catalog.TryGetObject(objectKey, out var obj))
        {
            return NotFound(new { error = $"unknown object '{objectKey}'" });
        }

        return Ok(obj.Events.Select(e => new EventDto(e.Key, e.Label, e.Description)));
    }

    /// <summary>
    /// Flattened object+event pairs. An alternative to two cascading dropdowns: a Zap
    /// can offer one "What happened?" dropdown backed by this list.
    /// </summary>
    [HttpGet("triggers")]
    public IActionResult GetTriggers()
    {
        var triggers = _catalog.GetObjects()
            .SelectMany(o => o.Events.Select(e => new TriggerDto(
                Key: $"{o.Key}.{e.Key}",
                Label: $"{o.Label}: {e.Label}",
                ObjectKey: o.Key,
                ObjectLabel: o.Label,
                EventKey: e.Key,
                EventLabel: e.Label,
                Description: e.Description)));

        return Ok(triggers);
    }
}
