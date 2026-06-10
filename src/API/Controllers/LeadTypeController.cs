using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;
using PI.Shared.Services;
using Services;

namespace Controllers
{
    [Authorize("default")]
    [Produces("application/json")]
    [Route("/api/v1/[controller]")]
    public class LeadTypeController : APIController
    {
        private readonly IMapper _mapper;
        private readonly LeadBuilderService _leadBuilderService;
        private readonly ILeadTypeAdapter _leadTypeAdapter;
        private readonly ILeadAdapter _leadAdapter;
        private readonly LeadTypeMapper _leadMapper;

        public LeadTypeController(
            IMapper mapper,
            LeadBuilderService leadBuilderService,
            ILeadTypeAdapter leadTypeAdapter,
            ILeadAdapter leadAdapter,
            LeadTypeMapper leadMapper
            )
        {
            this._mapper = mapper;
            this._leadBuilderService = leadBuilderService;
            this._leadTypeAdapter = leadTypeAdapter;
            this._leadAdapter = leadAdapter;
            this._leadMapper = leadMapper;
        }

        // [HttpGet]
        // [ProducesResponseType(typeof(IEnumerable<Models.LeadType>), 200)]
        // public async Task<IActionResult> GetAllAsync()
        // {
        //     var result = await _leadTypeAdapter.GetTrunkAsync(Context);
        //     var api = result.ToList().ConvertAll(l => _mapper.Map<Models.LeadType>(l));
        //     return Ok(api);
        // }

        // [HttpPost]
        // [ProducesResponseType(typeof(Models.LeadType), 200)]
        // public async Task<IActionResult> AddAsync([FromBody] LeadTypeToAdd lead)
        // {
        //     var context = Context;

        //     var entityId = default(Guid?);
        //     switch (context.Role)
        //     {
        //         case EntityRoleId.Manager:
        //             entityId = context.OrganizationId.Value;
        //             break;

        //         case EntityRoleId.Admin:
        //             entityId = context.AccountId.Value;
        //             break;

        //         case EntityRoleId.User:
        //             entityId = context.UserId.Value;
        //             break;

        //         default:
        //             return Forbid();
        //     }

        //     return await AddAsync(lead, entityId.Value);
        // }

        // [Authorize("Manager")]
        // [HttpPost("/api/v1/Organization/[controller]")]
        // [ProducesResponseType(typeof(LeadType), 200)]
        // public async Task<IActionResult> AddToOrgAsync([FromBody] LeadTypeToAdd lead)
        // {
        //     var user = this.AuthenticatedUser();
        //     return await AddAsync(lead, user.OrganizationId.Value);
        // }

        // [Authorize("Admin")]
        // [HttpPost("/api/v1/Account/[controller]")]
        // [ProducesResponseType(typeof(LeadType), 200)]
        // public async Task<IActionResult> AddToAccountAsync([FromBody] LeadTypeToAdd lead)
        // {
        //     var user = this.AuthenticatedUser();
        //     return await AddAsync(lead, user.AccountId.Value);
        // }

        // private async Task<IActionResult> AddAsync(LeadTypeToAdd lead, Guid entityId)
        // {
        //     if (lead == null || string.IsNullOrEmpty(lead.Name) || lead.Settings?.Fields == null) return BadRequest();

        //     var result = await _leadTypeAdapter.CreateAsync(new PI.Shared.Data.Models.LeadType
        //     {
        //         Id = lead.Id.GetValueOrDefault(Guid.NewGuid()),
        //         Name = lead.Name,
        //         AccountId = Context.AccountId.Value,
        //         EntityId = entityId,
        //         Settings = lead.Settings
        //     });

        //     return Ok(_mapper.Map<Models.LeadType>(result));
        // }

        [HttpGet("/api/v1/[controller]({id})")]
        [ProducesResponseType(typeof(Models.LeadType), 200)]
        public async Task<IActionResult> GetByIdAsync(Guid id)
        {
            var leadType = await _leadTypeAdapter.GetByIdAsync(id);
            if (leadType == null) return NotFound();

            // TODO: LIMIT ACCESS??
            // users need access to org leadtypes though... 
            // ...

            return Ok(_mapper.Map<Models.LeadType>(leadType));
        }

        [Authorize("managerplus")]
        [HttpDelete("/api/v1/[controller]({id})")]
        [ProducesResponseType(typeof(Models.LeadType), 200)]
        public async Task<IActionResult> DeleteAsync(Guid id)
        {
            var leadType = await _leadTypeAdapter.GetByIdAsync(id);
            if (leadType == null) return NotFound();
            if (!CanAccess(leadType))
            {
                return Forbid();
            }

            var result = await _leadTypeAdapter.DeleteAsync(id);
            if (!result)
            {
                // ...
                return new StatusCodeResult(500);
            }

            return Ok(_mapper.Map<Models.LeadType>(leadType));
        }

        [Authorize("managerplus")]
        [HttpPatch("/api/v1/[controller]({id})")]
        [ProducesResponseType(typeof(Models.LeadType), 200)]
        public async Task<IActionResult> UpdateAsync([FromRoute] Guid id, string name, Guid? flowId)
        {
            if (name == null && !flowId.HasValue) return BadRequest();

            var leadType = await _leadTypeAdapter.GetByIdAsync(id);
            if (leadType == null) return NotFound();
            if (!CanAccess(leadType))
            {
                return Forbid();
            }

            var mutable = _mapper.Map<PI.Shared.Data.Models.LeadType>(leadType);

            if (!string.IsNullOrWhiteSpace(name)) mutable.Name = name;
            if (flowId.HasValue)
            {
                // TODO: check if flow exists for entity
                // ...
                mutable.FlowId = flowId;
            }

            var success = await _leadTypeAdapter.UpdateAsync(mutable);
            if (!success) return StatusCode(500, "Update failure");
            return Ok(_mapper.Map<Models.LeadType>(leadType));
        }

        [HttpGet("/api/v1/[controller]({id})/Mapping")]
        [ProducesResponseType(typeof(LeadTypeSettings), 200)]
        public async Task<IActionResult> GetMappingAsync([FromRoute] Guid id)
        {
            var user = Context;

            var leadType = await _leadTypeAdapter.GetByIdAsync(id);
            if (leadType == null) return NotFound();
            if (!CanAccess(leadType))
            {
                return Forbid();
            }

            return Ok(leadType?.Settings ?? new LeadTypeSettings());
        }

        [HttpGet("/api/v1/[controller]({id})/Mapping/Refresh")]
        [ProducesResponseType(typeof(LeadTypeSettings), 200)]
        public async Task<IActionResult> CalculateMappingAsync([FromRoute] Guid id)
        {
            var leadType = await _leadTypeAdapter.GetByIdAsync(id);
            if (leadType == null) return NotFound();
            if (!CanAccess(leadType))
            {
                return Forbid();
            }

            var map = await _leadMapper.RefreshMapAsync(Context, leadType);
            var settings = leadType.Settings ?? new LeadTypeSettings();
            settings.Fields = map.ToArray();

            return Ok(settings);
        }

        // [AllowAnonymous]
        // [HttpGet("/api/v1/[controller]({id})/Mapping/Download")]
        // public async Task<FileContentResult> DownloadMappingAsync([FromRoute] Guid id)
        // {
        //     var ret = await RefreshMappingAsync(id);
        //     var settings = new JsonSerializerSettings {
        //         Formatting = Formatting.Indented,
        //         // DefaultValueHandling = DefaultValueHandling.Ignore,
        //         // NullValueHandling = NullValueHandling.Ignore
        //     };

        //     var str = JsonConvert.SerializeObject(ret,settings);
        //     return File(System.Text.Encoding.ASCII.GetBytes(str), "application/json", $"{id}.json");
        // }  


        [Authorize("managerplus")]
        [HttpPut("/api/v1/[controller]({id})/Mapping")]
        [ProducesResponseType(typeof(LeadTypeSettings), 200)]
        public async Task<IActionResult> AddMappingAsync([FromRoute] Guid id, [FromBody] LeadTypeSettings mapping)
        {
            if (mapping == null || mapping.Fields == null) return BadRequest();

            var leadType = await _leadTypeAdapter.GetByIdAsync(id);
            if (leadType == null) return NotFound();
            if (!CanAccess(leadType))
            {
                return Forbid();
            }

            await _leadTypeAdapter.UpdateSettingsAsync(leadType.Id, mapping);

            return Ok(mapping);
        }

        private bool CanAccess(LeadType leadType)
        {
            if (leadType == null) return false;

            // TODO: allow access to derived (e.g. admin to org's ...)
            // ...
            var user = Context;
            switch (user.Role)
            {
                case EntityRoleId.Manager:
                    if (!leadType.EntityId.Equals(user.UserId.Value) &&
                        !leadType.EntityId.Equals(user.OrganizationId.Value) &&
                        !leadType.EntityId.Equals(user.AccountId.Value))
                    {
                        return false;
                    }
                    break;

                case EntityRoleId.Admin:
                    if (!leadType.EntityId.Equals(user.UserId.Value) && !leadType.EntityId.Equals(user.AccountId.Value))
                    {
                        return false;
                    }
                    break;

                case EntityRoleId.Root:
                    break;

                default:
                    return false;
            }

            return true;
        }
    }
}