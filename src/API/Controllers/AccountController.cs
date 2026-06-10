// using System;
// using System.Linq;
// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using PI.Shared.Controllers;
// using PI.Shared.Data.Adapters;
//
// namespace Controllers
// {
//     [Authorize("admin")]
//     [Produces("application/json")]
//     [Route("/api/v1/[controller]")]
//     public class AccountController : APIController
//     {
//         [Obsolete]
//         [HttpPost("Identity({provider})")]
//         public async Task<IActionResult> UpdateIdentityAsync(string provider,
//             [FromServices] IEntityIdentityAdapter adapter,
//             [FromServices] IAccountAdapter accountAdapter,
//             [FromServices] IUserAdapter userAdapter)
//         {
//             // TODO: have to figure out a better way 
//             // the identity (externalid) is different between the account and the user
//             // but the token used is goind to be the one for a user
//             // ...
//             
//             var user = await userAdapter.GetByIdAsync(Context.EntityId.Value);
//             if (user == null) return NotFound();
//
//             var identity = user.GetIdentities()
//                 .Where(x => string.Equals(x.IdentityProviderId, provider) && x.ExternalIdentity?.Token != null)
//                 .FirstOrDefault();
//
//             if (string.IsNullOrEmpty(identity?.ExternalIdentity.Token.RefreshToken)) return NotFound("No Token");
//
//             var account = await accountAdapter.GetByIdAsync(user.AccountId);
//             var accountIdentity = account.GetIdentities()
//                 .Where(x => string.Equals(x.IdentityProviderId, provider))
//                 .FirstOrDefault();
//
//             if (accountIdentity?.ExternalIdentity == null) return BadRequest();
//             
//             accountIdentity.ExternalIdentity.Token = identity.ExternalIdentity.Token;
//             await adapter.UpdateTokenAsync(account, accountIdentity);
//
//             identity.ExternalIdentity.Token = null;
//             await adapter.UpdateTokenAsync(user, identity);
//
//             return Ok();
//         }       
//     }
// }