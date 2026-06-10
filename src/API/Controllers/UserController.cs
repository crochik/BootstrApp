using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;
using User = Controllers.Models.User;

namespace Controllers;

[Route("/api/v1/[controller]")]
public class UserController : APIController
{
    private readonly IMapper _mapper;
    private readonly MongoConnection _connection;
    private readonly IUserAdapter _userAdapter;
    private readonly IOrganizationAdapter _organizationAdapter;
    private readonly AppointmentSchedulerService _schedulerService;
    private readonly AuthorizationService _authorizationService;

    public UserController(
        IMapper mapper,
        MongoConnection connection,
        IUserAdapter userAdapter,
        IOrganizationAdapter organizationAdapter,
        AppointmentSchedulerService schedulerService,
        AuthorizationService authorizationService
    )
    {
        _mapper = mapper;
        _connection = connection;
        _userAdapter = userAdapter;
        _organizationAdapter = organizationAdapter;
        _schedulerService = schedulerService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// DataForm action to impersonate user
    /// </summary>
    [Authorize("managerplus")]
    [HttpPost("Impersonate/DataForm")] // NextUrl=action://api/v1/User/Impersonate without form (Request.Parameters will have user) 
    [HttpPost("/api/v1/[controller]({id})/Impersonate/DataForm")] // NextUrl=action://api/v1/User({{id}})/Impersonate without form (Request.Parameters will have user)
    [HttpPost("/api/v1/[controller]({id})/Impersonate/DataViewAction")] // NextUrl=action://api/v1/User({{id}})/Impersonate with form
    public async Task<DataFormActionResponse> ImpersonateUserActionAsync([FromBody] DataFormActionRequest request, [FromRoute] Guid? id)
    {
        if (!Request.Headers.TryGetValue("Origin", out var originHeaders))
        {
            return new DataFormActionResponse(request, "Missing Origin");
        }

        var origin = originHeaders.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return new DataFormActionResponse(request, "Invalid Origin");
        }

        id ??= request.TryGetGuidParam(Model.IdFieldName, out var idParam) ? idParam : null;
        if (!id.HasValue) return new DataFormActionResponse(request, "Missing User Id");

        var user = await _connection.Filter<Entity, PI.Shared.Models.User>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var result = await _authorizationService.ImpersonateUserAsync(Context, user, expiration: TimeSpan.FromMinutes(30));
        if (!result.IsSuccess)
        {
            return new DataFormActionResponse(request, result.Status);
        }

        // TODO: should check the client to figure out "home page"?
        // ...

        var token = Uri.EscapeDataString(result.Value);
        var url = origin[^1] != '/' ? $"{origin}/index.html#{result.Value}" : $"{origin}index.html#{token}";

        return new DataFormActionResponse(request, $"Impersonating {user.Name}", true)
        {
            NextUrl = url,
        };
    }

    /// <summary>
    /// DataForm action to create ghost user 
    /// </summary>
    [Authorize("admin")]
    [HttpPost("Ghost/DataForm")] // NextUrl=action://api/v1/User/Ghost without form (Request.Parameters will have user) 
    [HttpPost("/api/v1/[controller]({id})/Ghost/DataForm")] // NextUrl=action://api/v1/User({{id}})/Ghost without form (Request.Parameters will have user)
    [HttpPost("/api/v1/[controller]({id})/Ghost/DataViewAction")] // NextUrl=action://api/v1/User({{id}})/Ghost with form
    public async Task<DataFormActionResponse> CrateGhostUserActionAsync([FromBody] DataFormActionRequest request, [FromRoute] Guid? id)
    {
        id ??= request.TryGetGuidParam(Model.IdFieldName, out var idParam) ? idParam : null;
        if (!id.HasValue) return new DataFormActionResponse(request, "Missing User Id");

        var user = await _connection.Filter<Entity, PI.Shared.Models.User>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id.Value)
            .FirstOrDefaultAsync();

        if (user == null) return new DataFormActionResponse(request, "User not found");

        if (!request.TryGetGuidParam(nameof(PI.Shared.Models.User.OrganizationId), out var organizationId))
        {
            return new DataFormActionResponse(request, "Missing required OrganizationId");
        }

        var organization = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, organizationId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (organization == null) return new DataFormActionResponse(request, "Invalid Organization");

        if (!user.IsActive) return new DataFormActionResponse(request, "Can't create a ghost user for an Inactive User");
        if (user.OrganizationId == organization.Id) return new DataFormActionResponse(request, "Can't create a ghost user in the Organization the user belongs to");

        // make sure there isn't one user already
        var existing = await _connection.Filter<Entity, PI.Shared.Models.User>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.OrganizationId, organization.Id)
            .ElemMatchBuilder(
                f => f.Identities,
                q => q
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Bootstrapp))
                    .Eq(x => x.ExternalId, user.Id.ToString())
            )
            .FirstOrDefaultAsync();

        if (existing != null) return new DataFormActionResponse(request, $"There is already a ghost user for {user.Name} in {organization.Name}");

        existing = await _connection.InsertAsync(
            new PI.Shared.Models.User
            {
                Id = Guid.NewGuid(),
                AccountId = user.AccountId,
                OrganizationId = organization.Id,
                EntityId = organization.Id,
                Name = $"{user.Name} \ud83d\udc7b",
                Email = user.Email,
                Phone = user.Phone,
                TimeZoneId = user.TimeZoneId,
                Description = $"{user.Name} @ {organization.Name}",
                UserRoleId = nameof(EntityRoleId.Manager), // always creates managers for now
                Identities = new[]
                {
                    new EntityIdentity
                    {
                        IdentityProviderId = nameof(ExternalProvider.Bootstrapp),
                        Id = Guid.NewGuid(),
                        ExternalId = user.Id.ToString(),
                        Name = $"{user.Name} @ {organization.Name}",
                    }
                },
                IsActive = true,
                CreatedOn = DateTime.UtcNow,
                LastActor = Context.Actor,
            }
        );
        
        return new DataFormActionResponse(request, $"{existing.Description} Created", true);
    }

    [Authorize("default")]
    [HttpGet]
    public async Task<User> Me()
    {
        var user = await _userAdapter.GetByIdAsync(Context.UserId.Value);
        if (user == null) throw NotFoundException.New<PI.Shared.Models.User>(Context.UserId.Value);

        return _mapper.Map<User>(user);
    }

    [Authorize("default")]
    [HttpGet("/api/v1/[controller]({id})")]
    public async Task<User> GetByIdAsync([FromRoute] Guid id)
    {
        var user = await _userAdapter.GetByIdAsync(Context, id);
        if (user == null) throw NotFoundException.New<PI.Shared.Models.User>(id);

        return _mapper.Map<User>(user);
    }

    [Authorize("default")]
    [HttpDelete("/api/v1/[controller]")]
    public async Task<IActionResult> DeleteAsync()
    {
        var result = await _userAdapter.DeleteAsync(Context.UserId.Value);
        if (!result) return StatusCode(500, "Failed to Delete User");

        return Ok();
    }

    [Authorize("managerplus")]
    [HttpDelete("/api/v1/[controller]({id})")]
    public async Task<IActionResult> DeleteUserAsync([FromRoute] Guid id)
    {
        var user = await _userAdapter.GetByIdAsync(id);
        if (user == null) return NotFound();

        var me = Context;
        if (user.Id.Equals(me.UserId.Value)) return Forbid();

        switch (me.Role)
        {
            case EntityRoleId.Admin:
                if (!user.AccountId.Equals(me.AccountId.Value)) return Forbid();
                break;
            case EntityRoleId.Manager:
                if (!user.OrganizationId.HasValue || !user.OrganizationId.Equals(me.OrganizationId.Value)) return Forbid();
                break;
            case EntityRoleId.Root:
                // any
                break;

            default:
                return Forbid();
        }

        var result = await _userAdapter.DeleteAsync(id);
        if (!result) return StatusCode(500, "Failed to Delete User");

        return Ok();
    }

    // [Authorize("root")]
    // [HttpGet("/api/v1/Entity({entityId})/[controller]")]
    // [ProducesResponseType(typeof(IEnumerable<Models.User>), 200)]
    // public async Task<IActionResult> GetUsersAsync([FromRoute] Guid entityId)
    // {
    //     return await getUsersForEntityAsync(RootContext.Context);
    // }

    [Authorize("admin")]
    [HttpGet("/api/v1/Organization({organizationId})/[controller]")]
    [ProducesResponseType(typeof(IEnumerable<User>), 200)]
    public async Task<IActionResult> GetOrganizationUsersAsync([FromRoute] Guid organizationId)
    {
        var org = await _organizationAdapter.GetByIdAsync(organizationId);
        if (org == null) return NotFound();

        return await GetUsersForEntityAsync(org.Context);
    }

    [Authorize("manager")]
    [HttpGet("/api/v1/Organization/[controller]")]
    [ProducesResponseType(typeof(IEnumerable<User>), 200)]
    public async Task<IActionResult> GetMyOrganizationUsersAsync()
    {
        return await GetUsersForEntityAsync(Context);
    }

    [Authorize("default")]
    [HttpGet("/api/v1/Entity/[controller]")]
    [ProducesResponseType(typeof(IEnumerable<User>), 200)]
    public async Task<IActionResult> GetUsersAsync()
    {
        return await GetUsersForEntityAsync(Context);
    }

    [Authorize("default")]
    [HttpGet("/api/v1/AppointmentType({id})/[controller]")]
    [ProducesResponseType(typeof(IEnumerable<User>), 200)]
    public async Task<IActionResult> GetAvailableUsersAsync([FromRoute] Guid id)
    {
        // TODO: check whether the user has access to the appointment
        // ...

        var users = await _userAdapter.GetAvaialbleForAppointmentAsync(id);
        var list = users.ToList().ConvertAll(usr => _mapper.Map<User>(usr));

        return Ok(list);
    }

    // get users that can possibly be used to schedule an appointment for this lead
    [Authorize("default")]
    [HttpGet("/api/v1/Lead({leadId})/AppointmentType({appointmentTypeId})/[controller]")]
    [ProducesResponseType(typeof(IEnumerable<User>), 200)]
    public async Task<IActionResult> GetPossibleUsersAsync([FromRoute] Guid leadId, [FromRoute] Guid appointmentTypeId)
    {
        try
        {
            var users = await _schedulerService.GetPossibleUsersAsync(
                Context,
                leadId: leadId,
                appointmentTypeId: appointmentTypeId
            );
            var list = users.ToList().ConvertAll(usr => _mapper.Map<User>(usr));

            return Ok(list);
        }
        catch (AppointmentSchedulerException ex)
        {
            switch (ex.Error)
            {
                default:
                    return BadRequest();
            }
        }
    }

    [Authorize("admin")]
    [HttpGet("MergeCandidates")]
    [ProducesResponseType(typeof(IEnumerable<MergeUserCandidateMatch>), 200)]
    public async Task<IActionResult> GetMergeCandidatesAsync()
    {
        var user = Context;
        var candidates = await _userAdapter.GetMergeCandidatesAsync(user.AccountId.Value);

        return Ok(candidates);
    }

    [Authorize("admin")]
    [HttpPost("Merge")]
    [ProducesResponseType(typeof(User), 200)]
    public async Task<IActionResult> MergeUsersAsync(Guid[] entityIds)
    {
        var user = Context;
        var accountId = user.AccountId.Value;

        if (entityIds?.Length == 2)
        {
            // TODO: move into userservice?
            var user1 = await _userAdapter.GetByIdAsync(entityIds[0]);
            if (user1 == null) return NotFound($"User {entityIds[0]} not found");

            var user2 = await _userAdapter.GetByIdAsync(entityIds[1]);
            if (user2 == null) return NotFound($"User {entityIds[1]} not found");

            if (!user1.AccountId.Equals(accountId) || !user2.AccountId.Equals(accountId))
            {
                return Forbid();
            }

            PI.Shared.Models.User merged;
            if (user1.UserRoleId.Equals(EntityRoleId.Disabled.ToString()))
            {
                merged = await _userAdapter.MergeAsync(user2, user1);
            }
            else if (user2.UserRoleId.Equals(EntityRoleId.Disabled.ToString()))
            {
                merged = await _userAdapter.MergeAsync(user1, user2);
            }
            else
            {
                return BadRequest();
            }

            if (merged == null)
            {
                return StatusCode(500, "Failed to merge");
            }

            return Ok(_mapper.Map<User>(merged));
        }

        return BadRequest();
    }

    private async Task<IActionResult> GetUsersForEntityAsync(IEntityContext context)
    {
        if (!Context.CanAccess(context)) return Forbid();

        var users = await _userAdapter.GetAsync(context);
        var list = users.ToList().ConvertAll(user => _mapper.Map<User>(user));
        return Ok(list);
    }

    // [HttpGet("Me/Raw")]
    // public IActionResult Raw()
    // {
    //     return new JsonResult(from c in User.Claims select new { c.Type, c.Value });
    // }


    // [Authorize]
    // [HttpGet("/api/v1/[controller]")]
    // public async Task<IEnumerable<UserLoginInfo>> Get()
    // {
    //     var user = await _userManager.GetUserAsync(HttpContext.User);
    //     var logins = await _userManager.GetLoginsAsync(user);
    //     return logins;
    // }

    // // TODO: return array of userlogininfo
    // [Authorize]
    // [HttpGet("/api/v1/[controller]({provider})")]
    // [ProducesResponseType(typeof(UserLoginInfo), 200)]
    // public async Task<IActionResult> Get([FromRoute] ExternalProvider provider)
    // {
    //     // var t = await HttpContext.GetTokenAsync("Microsoft", "access_token");
    //     // var token = await _userManager.GetAuthenticationTokenAsync(user, "Microsoft", "access_token");

    //     var user = await _userManager.GetUserAsync(HttpContext.User);

    //     // this is going to return the first match 
    //     // TODO: use tokenstore
    //     var logins = await _userManager.GetLoginsAsync(user);
    //     foreach (var login in logins)
    //     {
    //         if (login.LoginProvider == provider.ToString()) return Ok(login);
    //     }

    //     return NotFound();
    // }

    // // [Authorize]
    // // [HttpGet("/api/v1/[controller]({provider})/AccessToken")]
    // private async Task<string> AccessToken([FromRoute] ExternalProvider provider)
    // {
    //     // this method will throw an exception (by design) if there is more than one 
    //     // identity for the same user/provider

    //     var user = await _userManager.GetUserAsync(HttpContext.User);
    //     var token = await _userManager.GetAuthenticationTokenAsync(user, provider.ToString(), "access_token");
    //     return token;
    // }

    // [Authorize]
    // [HttpGet("/api/v1/[controller](Microsoft)/Me")]
    // [ProducesResponseType(typeof(Microsoft.Graph.User), 200)]
    // public async Task<ActionResult> O365Me()
    // {
    //     var token = await AccessToken(ExternalProvider.Microsoft);
    //     if ( token==null ) return NotFound();

    //     var client = new Microsoft.Graph.GraphServiceClient(
    //         new Microsoft.Graph.DelegateAuthenticationProvider(
    //             (requestMessage) =>
    //             {
    //                 requestMessage.Headers.Authorization =
    //                     new AuthenticationHeaderValue("Bearer", token);

    //                 return Task.CompletedTask;
    //             }
    //         )
    //     );

    //     var me = await client.Me.Request().GetAsync();
    //     return Ok(me);
    // }
}