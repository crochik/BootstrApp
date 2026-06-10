using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Requests;
using Services;

namespace Controllers;

[Route("/api/v1/[controller]")]
public class U2Controller(RedirectionService redirectionService) : APIController
{
    /// <summary>
    /// Create Redirection for object
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/[controller]({shareTemplateId})/{objectTypeName}/{fieldName}/DataForm")]
    public async Task<DataFormActionResponse> CreateRedirectionAsync([FromRoute] string objectTypeName, [FromRoute] Guid shareTemplateId, [FromRoute] string fieldName, [FromBody] DataFormActionRequest request)
    {
        if (!request.TryGetGuidParam(Model.IdFieldName, out var objectId)) return DataFormActionResponse.Error(request, "Missing object Id");
        var result = await redirectionService.CreateRedirectionAsync(Context, objectTypeName, objectId, shareTemplateId, fieldName);
        if (result.IsSuccess)
        {
            var redirection = result.Value;
            return new DataFormActionResponse(request, "Redirected", true)
            {
                NextUrl = $"https://{redirection.Host}/{redirection.ShortCode}",
            };
        }
        
        return DataFormActionResponse.Error(request, result.Status);
    }
}