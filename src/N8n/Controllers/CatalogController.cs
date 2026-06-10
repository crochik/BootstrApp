using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Integrations.Catalog;

namespace N8n.Controllers;

/// <summary>
/// Feeds the n8n node's <c>loadOptions</c> dropdowns. These endpoints make the
/// integration generic: n8n reads the available objects and events here — discovered
/// from the account's real object types — at the moment a user configures the trigger
/// node, so nothing about the domain is baked into the node.
/// </summary>
[Authorize("n8n")]
[Route("/n8n/v1")]
public class CatalogController : APIController
{
    private readonly IObjectCatalog _catalog;

    public CatalogController(IObjectCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Lists every object — source for the "Object" dropdown (<c>loadOptions</c>).</summary>
    [HttpGet("objects")]
    public async Task<IActionResult> GetObjects()
    {
        var objects = await _catalog.GetObjectsAsync(Context);
        return Ok(objects.Select(o => new N8nOption(o.Label, o.Key, o.Description)));
    }

    /// <summary>Lists events for one object — source for the dependent "Event" dropdown.</summary>
    [HttpGet("objects/{objectKey}/events")]
    public async Task<IActionResult> GetEvents(string objectKey)
    {
        var obj = await _catalog.GetObjectAsync(Context, objectKey);
        if (obj is null)
        {
            return NotFound(new { error = $"unknown object '{objectKey}'" });
        }

        return Ok(obj.Events.Select(e => new N8nOption(e.Label, e.Key, e.Description)));
    }
}
