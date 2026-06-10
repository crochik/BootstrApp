using MCP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;

[Route("/BootstrApp/mcp/[controller]")]
public class DownloadController(
    SingleUseFileAccessService  service
    ) : APIController
{
    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<IActionResult> Download([FromRoute] Guid id)
    {
        var item = await service.GetAsync(id);
        if (item.IsError) return BadRequest(item.Status);

        return Content(item.Value.Content, item.Value.ContentType);
    }
}