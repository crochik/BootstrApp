using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization.Attributes;
using PI.DocuSeal.Models;
using PI.DocuSeal.Providers;
using PI.DocuSeal.Services;
using PI.ProductCatalog.Models;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Models.U2;
using PI.Shared.Services;
using Providers;

namespace Controllers;

[ApiController]
[Route("docuseal/v1/[controller]")]
public class ShareController(MongoConnection connection, ObjectTypeService objectTypeService) : APIController
{
    [AllowAnonymous]
    [HttpGet("/docuseal/v1/[controller]({objectId})/HTML")]
    public async Task<IActionResult> GetSubmissionHtmlAsync([FromRoute] Guid objectId, [FromServices] DocuSealService service)
    {
        var share = await connection.Filter<SingleResourceAccessToken, ViewEstimateToken>()
            .Eq(x => x.Id, objectId)
            .Eq(x => x.Parent.ObjectType, Estimate.ObjectTypeFullName)
            .OrBuilder(
                q => q.Eq(x => x.Expiration, null),
                q => q.Gt(x => x.Expiration, DateTime.UtcNow)
            )
            .Ne(x => x.IsActive, false)
            .Update
            .Inc(x => x.ViewCount, 1)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (share == null) return BadRequest("This share link has expired");
        if (!share.MetaValues?.TemplateId.HasValue ?? true) return BadRequest("Invalid Share (Template)");

        if (share.MaxViewCount.HasValue && share.MaxViewCount <= share.ViewCount)
        {
            // hit max views, disable it
            await connection.Filter<SingleResourceAccessToken, ViewEstimateToken>()
                .Eq(x => x.Id, objectId)
                .Update
                .Set(x => x.IsActive, false)
                .UpdateOneAsync();
        }

        // render on behalf of user
        var user = await connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, share.AccountId)
            .Eq(x => x.Id, share.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();
        
        if (user==null) return BadRequest("Invalid Share (User)");
        var context = user.Context;
        
        var documentTemplate = await connection.Filter<DocumentTemplate>()
            .Eq(x => x.AccountId, share.AccountId)
            .Eq(x => x.Id, share.MetaValues.TemplateId)
            .FirstOrDefaultAsync();

        if (documentTemplate == null) return BadRequest("Invalid or Missing Template");
        
        if (share.Parent.ObjectId is not string objectIdStr || !Guid.TryParse(objectIdStr, out var estimateId)) return BadRequest("Invalid Share (Estimate)"); 
        
        // load the object under account context so it can have access to all fields
        var docContext = await service.BuildObjectContextAsync(new AccountContext(share.AccountId), share.Parent.ObjectType, estimateId);
        if (docContext.IsError) return BadRequest("Invalid Share (Context)");

        // generate under user context
        var doc = await service.GenerateDocumentAsync(context, documentTemplate, docContext.Value);
        if (doc.IsError) return BadRequest(doc.Status);
        
        return Content(doc.Value.Content, "text/html");
    }
}

[BsonDiscriminator("ViewEstimate")]
public class ViewEstimateToken : SingleResourceAccessToken
{
    public ViewEstimateMetaValues? MetaValues { get; set; }

    public class ViewEstimateMetaValues
    {
        public Guid? TemplateId { get; set; }
    }
}