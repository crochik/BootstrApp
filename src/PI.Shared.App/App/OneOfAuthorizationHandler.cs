using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;

namespace PI.Shared.App.App;

public class OneOfClaimsRequirement : IAuthorizationRequirement
{
    public string[] ClaimTypes { get; }

    public OneOfClaimsRequirement(string[] claimTypes)
    {
        ClaimTypes = claimTypes;
    }
}

public class OneOfAuthorizationHandler : AuthorizationHandler<OneOfClaimsRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OneOfClaimsRequirement requirement)
    {
        if (context.User.Claims.Any(c => requirement.ClaimTypes.Contains(c.Type)))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}