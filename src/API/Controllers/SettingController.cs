using System;
using System.Threading.Tasks;
using AutoMapper;
using Controllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace Controllers;

[Route("/api/v1/[controller]")]
[Authorize("default")]
public class SettingController : APIController
{
    private readonly IMapper _mapper;
    private readonly IEntityIdentityAdapter _identityAdapter;

    public SettingController(
        IMapper mapper,
        IEntityIdentityAdapter identityAdapter
    )
    {
        this._mapper = mapper;
        this._identityAdapter = identityAdapter;
    }

    // [HttpGet("/api/v1/[controller]")]
    // [ProducesResponseType(typeof(IEnumerable<Setting>), 200)]
    // public  Task<IActionResult> GetAllSettingAsync()
    // {
    //     // merge results from different adapters
    //     // ...
    //     // var result = await _text.GetAsync(Context);
    //     // return Ok(result);

    //     throw new NotImplementedException();
    // }

    // [HttpGet("/api/v1/[controller]({setting})")]
    // [ProducesResponseType(typeof(Setting), 200)]
    // public  Task<IActionResult> GetSettingAsync([FromRoute] string setting)
    // {
    //     // get type to determine what adapter to use
    //     // ...

    //     // var result = await _text.GetAsync(Context.UserId.Value, setting);
    //     // return Ok(result);

    //     throw new NotImplementedException();
    // }

    // [HttpPut("/api/v1/[controller]({setting})")]
    // public Task<IActionResult> SetSettingAsync([FromRoute] string setting, string value)
    // {
    //     // VALIDATE: check if the setting exist and is text
    //     // ...
    //     // var result = await _text.SetAsync(Context.UserId.Value, setting, value);
    //     // return result ? (IActionResult)Ok() : NotFound();

    //     throw new NotImplementedException();
    // }


    [HttpPut("/api/v1/[controller](TimeZone)")]
    [ProducesResponseType(typeof(Models.TimeZone), 200)]
    public async Task<IActionResult> SetTimeZoneAsync(
        string value,
        [FromServices] IUserAdapter userAdapter,
        [FromServices] IOrganizationAdapter organizationAdapter
    )
    {
        // valida 
        TimeZoneInfo timeZone;
        try
        {
            timeZone = System.TimeZoneInfo.FindSystemTimeZoneById(value);
        }
        catch (Exception)
        {
            // can throw 'cant find' or System.Security.SecurityException. 
            return BadRequest(value);
        }

        var entityId = Context.Role switch {
            PI.Shared.Models.EntityRoleId.Manager => await organizationAdapter.SetTimeZoneIdAsync(Context, Context.OrganizationId.Value, value),
            _ => await userAdapter.SetTimeZoneIdAsync(Context, Context.UserId.Value, value)
        };

        return Ok(_mapper.Map<Models.TimeZone>(timeZone));            
    }

    [HttpPut("/api/v1/[controller](WorkingHours)")]
    [ProducesResponseType(typeof(Models.WorkingHoursSetting), 200)]
    public Task<IActionResult> SetWorkingHoursAsync([FromBody] WorkingHoursSetting value)
    {
        if (value == null || value.StartMinutes == 0 || value.EndMinutes == 0)
        {
            return Task.FromResult<IActionResult>(BadRequest());
        }

        switch (Context.Role)
        {
            case PI.Shared.Models.EntityRoleId.Manager:
                return SetWorkingHoursAsync(Context.OrganizationId.Value, value);

            default:
                return SetWorkingHoursAsync(Context.UserId.Value, value);
        }
    }

    // [Authorize("manager")]
    // [HttpPut("/api/v1/Organization/Setting({setting})")]
    // public Task<IActionResult> SetOrganizationSettingAsync([FromRoute] string settingId, [FromBody] string value)
    // {
    //     var user = this.AuthenticatedUser();
    //     // return await SetTimeZoneAsync(user.OrganizationId.Value, value);
    //     throw new NotImplementedException();
    // }

    // [Authorize("admin")]
    // [HttpPut("/api/v1/Account/Setting({setting})")]
    // public Task<IActionResult> SetAccountSettingAsync([FromRoute] string settingId, [FromBody] string value)
    // {
    //     var user = this.AuthenticatedUser();
    //     // return await SetTimeZoneAsync(user.AccountId.Value, value);
    //     throw new NotImplementedException();
    // }

    /* 
    [HttpGet("/api/v1/[controller](TimeZone)")]
    [ProducesResponseType(typeof(Models.TimeZone), 200)]
    public Task<IActionResult> TimeZoneAsync()
    {
        var user = this.AuthenticatedUser();
        return GetTimeZoneAsync(user.Id.Value);
    }

    [Authorize("manager")]
    [HttpGet("/api/v1/Organization/Setting(TimeZone)")]
    [ProducesResponseType(typeof(Models.TimeZone), 200)]
    public async Task<IActionResult> OrganizationTimeZoneAsync()
    {
        var user = this.AuthenticatedUser();
        if (!user.OrganizationId.HasValue) return BadRequest("No Organization");
        return await GetTimeZoneAsync(user.OrganizationId.Value);
    }

    [Authorize("admin")]
    [HttpGet("/api/v1/Account/Setting(TimeZone)")]
    [ProducesResponseType(typeof(Models.TimeZone), 200)]
    public async Task<IActionResult> AccountTimeZoneAsync()
    {
        var user = this.AuthenticatedUser();
        if (!user.AccountId.HasValue) return BadRequest("No Account");
        return await GetTimeZoneAsync(user.AccountId.Value);
    }
    */

    // [Authorize("manager")]
    // [HttpPut("/api/v1/Organization/Setting(TimeZone)")]
    // [ProducesResponseType(typeof(Models.TimeZone), 200)]
    // public async Task<IActionResult> SetOrganizationTimeZoneAsync(string value)
    // {
    //     var user = this.AuthenticatedUser();
    //     return await SetTimeZoneAsync(user.OrganizationId.Value, value);
    // }

    // [Authorize("admin")]
    // [HttpPut("/api/v1/Account/Setting(TimeZone)")]
    // [ProducesResponseType(typeof(Models.TimeZone), 200)]
    // public async Task<IActionResult> SetAccountTimeZoneAsync(string value)
    // {
    //     var user = this.AuthenticatedUser();
    //     return await SetTimeZoneAsync(user.AccountId.Value, value);
    // }

    private async Task<IActionResult> GetTimeZoneAsync(Guid entityId)
    {
        var entity = await _identityAdapter.GetEntityByIdAsync(entityId);
        return Ok(_mapper.Map<Models.TimeZone>(entity.GetTimeZoneInfo()));
    }

    private Task<IActionResult> SetWorkingHoursAsync(Guid entityId, WorkingHoursSetting setting)
    {
        // var json = JsonConvert.SerializeObject(setting, PI.Shared.Data.Models.SerializationSettings.Default);
        // await _setting.SetStringSetting(entityId, SettingId.WorkingHours, json);
        // return Ok(setting);

        throw new NotImplementedException();
    }
}