using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using Zapier.Models;

namespace Zapier.Controllers;

[Authorize("zapier")]
[Route("/zapier/v1/[controller]")]
public class UserController(ILogger<UserController> logger, MongoConnection connection) : APIController
{
    [HttpGet]
    public async Task<UserResponse> Me()
    {
        var user = await connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, Context.UserId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            logger.LogError("{UserId} is not active", Context.UserId);
            throw new NotFoundException("User");
        }

        return new UserResponse
        {
            Id = user.Id,
            OrganizationId = user.OrganizationId,
            Name = user.Name,
            RoleId = user.UserRoleId,
        };
    }
}