using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using PI.Shared.Models;

namespace Services;

public class GitHubIdentityProvider : AbstractIdentityProvider
{
    public override string Name => "GitHub";

    public GitHubIdentityProvider(IMapper mapper) : base(mapper)
    {
    }

    public override ValueTask<ExternalIdentity> GetTenantAsync(ExternalLoginInfo loginInfo, ExternalIdentity userIdentity)
    {
        // no tenant? 
        return ValueTask.FromResult<ExternalIdentity>(null);
    }
}