using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace Controllers
{
    [Authorize("default")]
    [Route("/api/v1/[controller]")]
    public class IdentityController : APIController
    {
        private readonly IMapper _mapper;
        private readonly IEntityIdentityAdapter _identityAdapter;

        public IdentityController(
            IMapper mapper,
            IEntityIdentityAdapter identityAdapter
            )
        {
            this._mapper = mapper;
            this._identityAdapter = identityAdapter;
        }

        [HttpGet]
        public async Task<IEnumerable<Controllers.Models.Identity>> IdentityAsync()
        {
            var user = Context;
            var identities = await _identityAdapter.GetByEntityAsync(user.UserId.Value);
            var list = identities.ToList().ConvertAll((src) => _mapper.Map<Controllers.Models.Identity>(src));
            return list;
        }

        [HttpGet("Trunk")]
        public async Task<IEnumerable<TrunkIdentity>> IdentityTrunkAsync()
        {
            var user = Context;
            var identities = await _identityAdapter.GetEntityTrunkIdentitiesAsync(user.UserId.Value);
            return identities;
        }

        [HttpGet("/api/v1/[controller]@{provider}")]
        [ProducesResponseType(typeof(IEnumerable<Controllers.Models.Identity>), 200)]
        public async Task<IActionResult> GetIdentityAsync([FromRoute] ExternalProvider provider)
        {
            var user = Context;
            var identities = await _identityAdapter.GetByEntityAsync(user.UserId.Value, (ExternalProvider)provider);
            var list = identities.ToList().ConvertAll((src) => _mapper.Map<Controllers.Models.Identity>(src));
            return Ok(list);
        }

        [Authorize("admin")]
        [HttpGet("/api/v1/[controller]({externalId})@{provider}")]
        [ProducesResponseType(typeof(IEnumerable<Controllers.Models.Identity>), 200)]
        public async Task<IActionResult> FindIdentityAsync([FromRoute] ExternalProvider provider, [FromRoute] string externalId)
        {
            var row = await _identityAdapter.FindAsync(Context, provider, externalId);
            var result =  _mapper.Map<Controllers.Models.Identity>(row.Item2);
            return Ok(result);
        }

        //[HttpPost("/api/v1/[controller]@{provider}")]
        //[ProducesResponseType(typeof(Identity), 200)]
        //public async Task<IActionResult> AddCredentialsAsync([FromRoute] ExternalProvider provider, ClientCredentials clientCredentials) {
        //    var user = this.AuthenticatedUser();
        //    var identities = await _identityAdapter.GetByEntityAsync(user.Id.Value, (ExternalProvider)provider);
        //    if ( identities.Any() ) {
        //        // only one per type
        //        return Forbid();
        //    }

        //    var identity = await _identityAdapter.AddAsync(new PI.Shared.Data.Models.IdentityDAO {
        //        Id = Guid.NewGuid(),
        //        EntityId = user.Id.Value,
        //        IdentityProviderId = provider.ToString(),
        //        ExternalId = clientCredentials.ClientId
        //    });

        //    // TODO: use different model for clientcredentials?
        //    // ....
        //    var result = await _identityAdapter.UpdateValueAsync(
        //        identity.Id,
        //        new ExternalIdentity {
        //            Provider = provider,
        //            ExternalId = clientCredentials.ClientId,
        //            Token = new Token {
        //                AccessToken = clientCredentials.ClientSecret
        //            } 
        //        }
        //    );

        //    return Ok(_mapper.Map<Identity>(identity));
        //}
    }
}
