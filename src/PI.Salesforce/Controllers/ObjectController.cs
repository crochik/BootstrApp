using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Salesforce;
using PI.Shared.Services;
using Services;

namespace Controllers;

[Route("/salesforce/v1/[controller]")]
public class ObjectController : APIController
{
    private readonly SalesforceService _service;

    public ObjectController(
        SalesforceService service
    )
    {
        _service = service;
    }

    [Authorize("admin")]
    [HttpPost("query")]
    // [ProducesResponseType(typeof(ObjectMetaData), 200)]
    public async Task<IActionResult> GetAsync()
    {
        var sql = Request.GetBody();
        var result = await _service.QueryAllAsync<dynamic>(Context, sql);
        return result != null ? Ok(result) : NotFound();
    }

    [Authorize("admin")]
    [HttpGet("{objectTypeName}({id})")]
    public async Task<IActionResult> GetObjectAsync([FromRoute] string objectTypeName, [FromRoute] string id)
    {
        var result = await _service.GetObjectAsync(Context, objectTypeName, id);
        return result != null ? (IActionResult)Ok(result) : NotFound();
    }

    [Authorize("admin")]
    [HttpPatch("Lead({id})")]
    public async Task<IActionResult> LoadLeadObjectAsync([FromRoute] string id, [FromServices] IOnLeadChangeProcessor processor)
    {
        // var result = await _service.LoadObjectAsync(Context, objectTypeName, id);
        var (sf,imp) = await processor.ProcessChangeAsync(AccountIds.FCI, id, null);
        return imp != null ? (IActionResult)Ok(imp) : NotFound();
    }
    
    [Authorize("admin")]
    [HttpPatch("WorkOrder({id})")]
    public async Task<IActionResult> LoadWorkOrderAsync([FromRoute] string id, [FromServices] IOnWorkOrderChangeProcessor processor)
    {
        // var result = await _service.LoadObjectAsync(Context, objectTypeName, id);
        var (sf,imp) = await processor.ProcessChangeAsync(AccountIds.FCI, id, null);
        return imp != null ? (IActionResult)Ok(imp) : NotFound();
    }
}