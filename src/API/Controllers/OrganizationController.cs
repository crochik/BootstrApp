using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;
using Organization = Controllers.Models.Organization;

namespace Controllers;

[Authorize("default")]
[Route("/api/v1/[controller]")]
public class OrganizationController : APIController
{
    private readonly IMapper _mapper;
    private readonly IOrganizationAdapter _organizationAdapter;

    public OrganizationController(
        IMapper mapper,
        IOrganizationAdapter organizationAdapter
    )
    {
        _mapper = mapper;
        _organizationAdapter = organizationAdapter;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/[controller]")]
    [ProducesResponseType(typeof(Organization), 200)]
    public async Task<IActionResult> GetOrganizationAsync()
    {
        switch (Context.Role)
        {
            case EntityRoleId.Manager:
            case EntityRoleId.User:
                break;
                    
            default:
                return Forbid();
        }

        var result = await _organizationAdapter.GetByIdAsync(Context, Context.OrganizationId.Value);
        return Ok(_mapper.Map<Organization>(result));
    }

    [Authorize("admin")]
    [HttpPost("/api/v1/[controller]")]
    [ProducesResponseType(typeof(Organization), 200)]
    public async Task<IActionResult> AddOrganization([FromBody] AddOrganizationRequest request)
    {
        if (string.IsNullOrEmpty(request?.Name)) return BadRequest();

        var result = await _organizationAdapter.CreateAsync(
            Context,
            new PI.Shared.Models.Organization
            {
                Name = request.Name,
                TimeZoneId = request.TimeZoneId
            },
            null
        );

        return Ok(_mapper.Map<Organization>(result));
    }

    [Authorize("default")]
    [HttpGet("/api/v1/[controller]({id})")]
    [ProducesResponseType(typeof(Organization), 200)]
    public async Task<IActionResult> GetOrganizationByIdAsync([FromRoute] Guid id)
    {
        var organization = await _organizationAdapter.GetByIdAsync(Context, id);
        return organization != null ? Ok(_mapper.Map<Organization>(organization)) : NotFound();
    }

    [Authorize("root")]
    [HttpGet("/api/v1/Account({accountId})/[controller]")]
    [ProducesResponseType(typeof(IEnumerable<Organization>), 200)]
    public async Task<IActionResult> GetOrganizationsByAccountAsync([FromRoute] Guid accountId)
    {
        var result = await _organizationAdapter.GetByAccountAsync(accountId);
        var list = result.Select(x => _mapper.Map<Organization>(x));

        return Ok(list);
    }

    [Authorize("admin")]
    [HttpGet("/api/v1/Account/[controller]")]
    [ProducesResponseType(typeof(IEnumerable<Organization>), 200)]
    public Task<IActionResult> GetOrganizationsAsync()
    {
        var user = Context;
        return GetOrganizationsByAccountAsync(user.AccountId.Value);
    }

    public class AddOrganizationRequest
    {
        public string Name { get; set; }
        public string TimeZoneId { get; set; }
    }
}