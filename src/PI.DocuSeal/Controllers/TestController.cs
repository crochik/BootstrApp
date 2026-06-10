using System.Dynamic;
using Crochik.Mongo;
using Microsoft.AspNetCore.Mvc;
using PI.DocuSeal.Models;
using PI.DocuSeal.Services;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Models.Interfaces;

namespace PI.DocuSeal.Controllers;

#if DEBUG
[ApiController]
[Route("docuseal/v1/[controller]")]
public class TestController(DocuSealService service) : APIController
{
    [HttpGet("{objectTypeName}({objectId})/Template")]
    public async Task<IActionResult> RenderLocalTemplateAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId)
    {
        var context = UserContext.Admin(Guid.Parse("6abdf124-7a23-4091-be78-7bfaf175558b"), "Felipe Manager", AccountIds.FCI);

        var documentTemplate = new DocumentTemplate
        {
            ContentType = "text/html",
            Template = await System.IO.File.ReadAllTextAsync("/Users/felipe/DEVELOPMENT/github/SchedOnl/PI.DocuSeal/estimate_template.html"),
            StoredProcedures = new Dictionary<string, string>
            {
                { "getUser", "report.GetUser" },
                { "getProposal", "report.GetEstimate" },
            },
            Inputs = new Dictionary<string, string>
            {
                { "userId", "{{context \"UserId\"}}" },
                { "id", "{{Object._id}}" }
            },
        };
        
        var doc = await service.GenerateDocumentAsync(context, objectTypeName, objectId, documentTemplate);
        if (doc.IsError) return BadRequest(doc.Status);

        var submission = new DocuSealSubmission
        {
            Name = "Test Proposal for Signature",
            TemplateId = Guid.Empty,
            Parent = new ReferencedObject
            {
                ObjectType = objectTypeName,
                ObjectId = objectId,
            },
            Submitters =
            [
                new DocuSealSubmitter
                {
                    Name = "Felipe Crochik",
                    Email = "test+docuseal@b2-4ac.com",
                    Role = "Customer",
                },
            ],
            Content = doc.Value.Content,
            ContentType = doc.Value.ContentType,
        };

        var result = await service.CreateSubmissionAsync(context, submission, new ExpandoObject());
        if (result.IsError) return BadRequest(result.Status);

        var slug = result.Value.Submitters.FirstOrDefault()?.Slug;
        if (slug==null) return  BadRequest("No slug for submitter");
        
        var html = service.CreateHtml(slug);
        return Content(html, documentTemplate.ContentType);
    }
    
    [HttpGet("{objectType}({objectId})/DocumentTemplate({templateId})")]
    public async Task<IActionResult> CreateSubmissionAsync([FromRoute] string objectType, [FromRoute] Guid objectId, [FromRoute] Guid templateId, [FromServices] MongoConnection connection)
    {
        var context = UserContext.Admin(Guid.Parse("6abdf124-7a23-4091-be78-7bfaf175558b"), "Felipe Manager", AccountIds.FCI);
        
        var documentTemplate = await connection.Filter<DocumentTemplate>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            // .Eq(x => x.EntityId, Context.EntityId.Value)
            .Eq(x => x.Id, templateId)
            .FirstOrDefaultAsync();

        if (documentTemplate == null) return BadRequest("Invalid or Missing Template");
        
        var doc = await service.GenerateDocumentAsync(context, objectType, objectId, documentTemplate);
        if (doc.IsError) return BadRequest(doc.Status);

        return Content(doc.Value.Content, documentTemplate.ContentType);
    }    
}
#endif
