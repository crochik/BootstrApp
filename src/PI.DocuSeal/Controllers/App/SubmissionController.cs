using System.Dynamic;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.DocuSeal.Models;
using PI.DocuSeal.Services;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Models.Interfaces;
using PI.Shared.Requests;

namespace Controllers.App;

[ApiController]
[Authorize("rest")]
[Route("docuseal/api/[controller]")]
public class SubmissionController(MongoConnection connection, DocuSealService service) : APIController
{
    /// <summary>
    /// Create submissioon as part of 
    /// </summary>
    [HttpPost("{objectType}/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> CreateSubmissionAsync([FromBody] DataFormActionRequest<CreateSubmissionRequest> request, [FromRoute] string objectType, [FromRoute] Guid eventId)
    {
        if (request.SelectedIds == null || request.SelectedIds.Length != 1) return DataFormActionResponse.Error(request, "Missing Id");
        var objectId = request.SelectedIds.First();
        
        // TODO: check whether the event is valid for the object status 
        // ....
        
        
        // TODO: limit templates by org?
        // .... 
        var documentTemplate = await connection.Filter<DocumentTemplate>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            // .Eq(x => x.EntityId, Context.EntityId.Value)
            .Eq(x => x.Id, request.Parameters.TemplateId)
            .FirstOrDefaultAsync();

        if (documentTemplate == null) return DataFormActionResponse.Error(request, "Invalid or Missing Template");
        
        var doc = await service.GenerateDocumentAsync(Context, objectType, objectId, documentTemplate);
        if (doc.IsError) return DataFormActionResponse.Error(request, doc.Status);
        
        var submission = new DocuSealSubmission
        {
            Name = request.Parameters.Name,
            TemplateId = request.Parameters.TemplateId,
            Content = doc.Value.Content,
            ContentType = doc.Value.ContentType,
            Parent = new ReferencedObject
            {
                ObjectType = objectType,
                ObjectId = objectId,
            },
            Submitters =
            [
                new DocuSealSubmitter
                {
                    Name = request.Parameters.SubmitterName,
                    Email = request.Parameters.SubmitterEmail,
                    Role = request.Parameters.SubmitterRole,
                },
            ],
        };

        var result = await service.CreateSubmissionAsync(Context, submission, new ExpandoObject());
        if (result.IsError) return DataFormActionResponse.Error(request, result.Status);

        return new DataFormActionResponse(request)
        {
            Action = request.Action ?? "CreateSubmission",
            Success = true,
            Ids =
            [
                result.Value.Id
            ],
        };
    }
}

public class CreateSubmissionRequest
{
    public string Name { get; set; }
    public Guid TemplateId { get; set; }
    public string SubmitterName { get; set; }
    public string SubmitterEmail { get; set; }
    public string SubmitterRole { get; set; }
}