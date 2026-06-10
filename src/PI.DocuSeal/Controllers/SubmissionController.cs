using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.DocuSeal.Models;
using PI.DocuSeal.Services;
using PI.Shared.Controllers;

namespace Controllers;

[ApiController]
[Route("docuseal/v1/[controller]")]
public class SubmissionController(MongoConnection connection, DocuSealService service) : APIController
{
    [AllowAnonymous]
    [HttpGet("/docuseal/v1/[controller]({objectId})/HTML")]
    public async Task<IActionResult> GetSubmissionHtmlAsync([FromRoute] Guid objectId, [FromServices] MongoConnection connection)
    {
        var submission = await connection.Filter<DocuSealSubmission>()
            .Eq(x => x.Id, objectId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();
        
        var html = service.CreateHtml(submission);
        return Content(html, "text/html");
    }
}