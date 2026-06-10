using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.O365;

namespace Services;

public class MicrosoftIdentityProvider(IMapper mapper, O365AuthClient o365Client) : AbstractIdentityProvider(mapper)
{
    public override string Name => nameof(ExternalProvider.Microsoft);

    public override async ValueTask<ExternalIdentity> GetTenantAsync(ExternalLoginInfo loginInfo, ExternalIdentity userIdentity)
    {
        if (userIdentity.Token?.AccessToken == null)
        {
            // no token, can't get organization
            
            return null;
        }

        // need to make sure to exclude shared ones (like hotmail)
        // For personal accounts, the value is 9188040d-6c67-4c5b-b112-36a304b66dad
        // ...

        // get org 
        var client = await o365Client.GetClientAsync(userIdentity);
        var orgs = await client.Organization.Request().GetAsync();
        if (orgs.Count != 1) throw new NotFoundException("Couldn't determine Microsoft organization for user");

        var tenantIdentity = new ExternalIdentity
        {
            Provider = nameof(ExternalProvider.Microsoft),
            ExternalId = orgs[0].Id,
            Name = orgs[0].DisplayName,
        };

        return tenantIdentity;
    }
}