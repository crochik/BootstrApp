using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Authorize("rest")]
// [ApiExplorerSettings(IgnoreApi = true)]
[ApiExplorerSettings(GroupName = "rest")]
[Route("/app/api/User")]
public class ApiUserController(MongoConnection connection, AuthorizationService authorizationService) : APIController
{
    /// <summary>
    /// DataForm action to impersonate user
    /// will return the token as the next url ....
    /// hack but ... 
    /// </summary>
    [HttpGet("/app/api/User({id})/Impersonate")] // .../DataForm?
    [UseApiNames]
    public async Task<DataFormActionResponse> ImpersonateUserActionAsync([FromRoute] Guid id)
    {
        var request = new DataFormActionRequest
        {
            Action ="Impersonate",
            SelectedIds = [id],
        };
        
        var user = await connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var result = await authorizationService.ImpersonateUserAsync(Context, user, expiration: TimeSpan.FromMinutes(60));
        if (!result.IsSuccess)
        {
            return new DataFormActionResponse(request, result.Status);
        }

        return new DataFormActionResponse(request, $"Impersonating {user.Name}", true)
        {
            NextUrl = result.Value,
        };
    }
}