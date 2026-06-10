using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Services;

namespace Controllers;

[Authorize("admin")]
[Route("/salesforce/v1/[controller]")]
public class TestController : APIController
{
    private readonly SalesforceService _service;

    public TestController(
        SalesforceService service
    )
    {
        _service = service;
    }

    [HttpGet("Token")]
    public async Task<IActionResult> TokenAsync()
    {
        var (token, error) = await _service.GetTokenAsync(Context, true);
        if (token == null) return BadRequest(error);

        return Ok(new
        {
            token.ApiVersion,
            token.InstanceUrl,
            token.TokenType
        });
    }

    // [HttpGet("{objectTypeName}/Load")]
    // public async Task<IActionResult> LoadAsync([FromRoute] string objectTypeName, [FromServices] IEnumerable<IDataLoader> loaders)
    // {
    //     var accountContext = new AccountContext(AccountIds.FCI)
    //         .With(new SingerSyncActor(Guid.NewGuid()));
    //
    //     var loader = loaders.FirstOrDefault(x => x.ObjectTypeName == objectTypeName);
    //     if (loader == null) throw new BadRequestException($"{objectTypeName}: don't know how to load it");
    //     await loader.LoadAsync(accountContext);
    //
    //     return Ok();
    // }

    // [HttpGet("TopIdentity")]
    // public async Task<IActionResult> TopIdentityAsync([FromServices] SalesforceService service)
    // {
    //     var identity = (await service.GetTopIdentityAsync(Context.EntityId.Value)).Identity;
    //     return identity == null ?
    //         (IActionResult)NotFound() :
    //         Ok(new
    //         {
    //             identity.Id,
    //             identity.ExternalId,
    //             identity.IdentityProviderId,
    //             identity.Name
    //         });
    // }

    // [HttpGet("Entity")]
    // public async Task<IActionResult> TopEntityAsync([FromServices] IEntityIdentityAdapter adapter)
    // {
    //     var salesforce = ExternalProvider.Salesforce.ToString();
    //     var entities = await adapter.GetEntityTrunkAsync(Context);
    //     if (entities == null) return NotFound();

    //     var result = entities
    //         .OrderByDescending(x => x.ObjectType)
    //         .Select(x => (x, x.FirstIdentity(salesforce)))
    //         .Where(x => x.Item2 != null)
    //         .Select(x => new
    //         {
    //             x.Item1.Id,
    //             x.Item1.ObjectType,
    //             x.Item1.Name,
    //             IdentityId = x.Item2.Id,
    //             x.Item2.ExternalId,
    //             x.Item2.IdentityProviderId,
    //             Identity = x.Item2.Name
    //         });

    //     return Ok(result);
    // }
}