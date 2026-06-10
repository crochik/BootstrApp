using System;
using System.Threading.Tasks;
using Adapters;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using PI.Shared.Controllers;

namespace Controllers
{
    [Authorize("admin")]
    [Produces("application/json")]
    [Route("/singer/v1/[controller]")]
    public class ConfigController : APIController
    {
        private readonly IMapper _mapper;
        private readonly ILogger<ConfigController> _logger;
        private readonly ISingerConfigAdapter _adapter;

        public ConfigController(
            IMapper mapper,
            ILogger<ConfigController> logger,
            ISingerConfigAdapter adapter
            )
        {
            this._mapper = mapper;
            this._logger = logger;
            this._adapter = adapter;
        }

        [HttpGet()]
        [ProducesResponseType(typeof(SingerImportConfig), 200)]
        public async Task<IActionResult> GetAsync()
        {
            var list = await _adapter.GetAsync(Context);

            foreach (var config in list)
            {
                // remove sensitive information
                config.TapConfig = null;
            }

            return Ok(list);
        }

        [HttpGet("({id})")]
        [ProducesResponseType(typeof(SingerImportConfig), 200)]
        public async Task<IActionResult> GetByIdAsync([FromRoute] Guid id)
        {
            // TODO: map out (the default json is in the format used by singer)
            // ...
            var result = await _adapter.GetByIdAsync(id);
            if (result == null) return NotFound();

            return result != null ? (IActionResult)Ok(result) : NotFound();
        }
    }
}