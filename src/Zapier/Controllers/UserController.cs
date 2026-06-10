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
public class UserController : APIController
{
    private readonly ILogger<UserController> _logger;
    private readonly MongoConnection _connection;

    public UserController(ILogger<UserController> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    [HttpGet]
    public async Task<UserResponse> Me()
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, Context.UserId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            _logger.LogError("{UserId} is not active", Context.UserId);
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