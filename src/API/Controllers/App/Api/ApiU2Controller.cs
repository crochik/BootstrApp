using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Requests;
using Services;

namespace Controllers;

[Authorize("rest")]
// [ApiExplorerSettings(IgnoreApi = true)]
[ApiExplorerSettings(GroupName = "rest")]
[Route("/api/api/u2")]
public class ApiU2Controller(RedirectionService redirectionService) : APIController
{
    /// <summary>
    /// Create Redirection for object
    /// </summary>
    [HttpPost("/app/api/u2({shareTemplateId})/{objectTypeName}/{fieldName}/DataForm")]
    [UseApiNames]
    public async Task<DataFormActionResponse> CreateRedirectionAsync([FromRoute] string objectTypeName, [FromRoute] Guid shareTemplateId, [FromRoute] string fieldName, [FromBody] DataFormActionRequest request)
    {
        if (!request.TryGetGuidParam(Model.IdFieldName, out var objectId))
        {
            return DataFormActionResponse.Error(request, "Missing object Id");
        }
        
        var result = await redirectionService.CreateRedirectionAsync(Context, objectTypeName, objectId, shareTemplateId, fieldName);
        var response = result.IsSuccess
            ? new DataFormActionResponse(request, "Redirected", true)
            {
                NextUrl = $"https://{result.Value.Host}/{result.Value.ShortCode}",
            }
            : DataFormActionResponse.Error(request, result.Status);

        return response;
    }
}