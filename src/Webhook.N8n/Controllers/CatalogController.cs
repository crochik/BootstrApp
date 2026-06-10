using Microsoft.AspNetCore.Mvc;
using Webhook.Integrations.Core.Catalog;

namespace Webhook.N8n.Controllers;

/// <summary>
/// Feeds the n8n node's <c>loadOptions</c> dropdowns. These endpoints are what make
/// the integration generic: n8n reads the available objects and events here, at the
/// moment a user configures the trigger node, so nothing about the domain is baked
/// into the node.
/// </summary>
[ApiController]
[Route("n8n")]
public sealed class CatalogController : ControllerBase
{
    private readonly IEventCatalog _catalog;

    public CatalogController(IEventCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Lists every object — source for the "Object" dropdown (<c>loadOptions</c>).</summary>
    [HttpGet("objects")]
    public IActionResult GetObjects() =>
        Ok(_catalog.GetObjects().Select(o => new N8nOption(o.Label, o.Key, o.Description)));

    /// <summary>Lists events for one object — source for the dependent "Event" dropdown.</summary>
    [HttpGet("objects/{objectKey}/events")]
    public IActionResult GetEvents(string objectKey)
    {
        if (!_catalog.TryGetObject(objectKey, out var obj))
        {
            return NotFound(new { error = $"unknown object '{objectKey}'" });
        }

        return Ok(obj.Events.Select(e => new N8nOption(e.Label, e.Key, e.Description)));
    }
}
