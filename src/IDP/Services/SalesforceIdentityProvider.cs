using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace Services;

public class SalesforceIdentityProvider : AbstractIdentityProvider
{
    public override string Name => "Salesforce";

    public SalesforceIdentityProvider(IMapper mapper) : base(mapper)
    {
    }
    
    public override ValueTask<ExternalIdentity> GetIdentityAsync(ExternalLoginInfo loginInfo)
    {
        // ProviderKey "https://login.salesforce.com/id/00D41000002kXaPEAU/0051L00000BBmyLQAT"
        var parts = loginInfo.ProviderKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || !parts[2].Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Unexpected format");
        }
        
        var userIdentity = _mapper.Map<ExternalIdentity>(loginInfo);
        
        // replace sub with just the id 
        userIdentity.ExternalId = parts[4];

        return ValueTask.FromResult(userIdentity);
    }

    public override ValueTask<ExternalIdentity> GetTenantAsync(ExternalLoginInfo loginInfo, ExternalIdentity userIdentity)
    {
        // ProviderKey "https://login.salesforce.com/id/00D41000002kXaPEAU/0051L00000BBmyLQAT"
        var parts = loginInfo.ProviderKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || !parts[2].Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Unexpected format");
        }

        var tenantIdentity = new ExternalIdentity
        {
            Provider = nameof(ExternalProvider.Salesforce),
            ExternalId = parts[3],
            Name = null, //  do not have in the claims?
        };

        return ValueTask.FromResult(tenantIdentity);
    }
}