using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adapters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using PI.Shared.Controllers;
using Services;

namespace Controllers
{
    [Authorize("admin")]
    [Produces("application/json")]
    [Route("/singer/v1/[controller]")]
    public class JobController : APIController
    {
        private readonly ILogger<JobController> _logger;
        private readonly SingerService _service;
        private readonly ISingerConfigAdapter _adapter;

        public JobController(
            ILogger<JobController> logger,
            SingerService service,
            ISingerConfigAdapter adapter
            )
        {
            this._logger = logger;
            this._service = service;
            this._adapter = adapter;
        }

        [HttpGet("({id})")]
        [ProducesResponseType(typeof(SingerJob), 200)]
        public async Task<IActionResult> GetByIdAsync(Guid id)
        {
            var result = await _adapter.GetJobByIdAsync(id);
            if (result == null) return NotFound();
            if (result.AccountId != Context.AccountId.Value) return Forbid();

            return Ok(result);
        }

        [HttpGet("({id})/Summary")]
        [ProducesResponseType(typeof(IEnumerable<SingerJobSummary>), 200)]
        public async Task<IActionResult> GetSummaryByIdAsync(Guid id)
        {
            var job = await _adapter.GetJobByIdAsync(id);
            if (job == null) return NotFound();
            if (job.AccountId != Context.AccountId.Value) return Forbid();

            var result = await _adapter.GetJobSummaryAsync(job.Id);
            if (result == null) return NotFound();

            return Ok(result);
        }

        [HttpPost("/singer/v1/Config({id})/[controller]")]
        public async Task<IActionResult> RunAsync(Guid id)
        {
            var config = await _adapter.GetByIdAsync(id);
            if (config == null) return NotFound();
            if (config.AccountId != Context.AccountId.Value) return Forbid();

            _ = Task.Run(async () =>
            {
                await _service.ProcessAsync(Context, config);
            });

            return Ok();
        }

        [HttpPost("/singer/v1/Config({id})/Extract")]
        public async Task<IActionResult> ExtractAsync(Guid id)
        {
            var config = await _adapter.GetByIdAsync(id);
            if (config == null) return NotFound();
            if (config.AccountId != Context.AccountId.Value) return Forbid();

            _ = Task.Run(async () =>
            {
                await _service.ExtractAsync(Context, config);
            });

            return Ok();
        }

        [HttpPost("({id})/Load")]
        public async Task<IActionResult> ReplayAsync(Guid id)
        {
            var job = await _adapter.GetJobByIdAsync(id);
            if (job == null) return NotFound();
            if (job.AccountId != Context.AccountId.Value) return Forbid();

            _ = Task.Run(async () =>
            {
                await _service.LoadAsync(Context, job);
            });

            return Ok();
        }

        [HttpPost("({id})/Simulate")]
        public async Task<IActionResult> SimulateLoadingAsync(Guid id, string filename)
        {
            var job = await _adapter.GetJobByIdAsync(id);
            if (job == null) return NotFound();
            if (job.AccountId != Context.AccountId.Value) return Forbid();

            await _service.LoadFileAsync(Context, job, filename);

            return Ok();
        }

        /// <summary>
        /// hack for now to update 
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        [HttpPost("({id})/Transform")]
        public async Task<IActionResult> AfterJobAsync(Guid id)
        {
            var job = await _adapter.GetJobByIdAsync(id);
            if (job == null) return NotFound();
            if (job.AccountId != Context.AccountId.Value) return Forbid();

            var result = await _service.TransformAsync(job);
            return Ok(result);
        }

        [HttpGet("({id})/FireEvents")]
        public async Task<IActionResult> GetTransactionsAsync(Guid id)
        {
            var job = await _adapter.GetJobByIdAsync(id);
            if (job == null) return NotFound();
            if (job.AccountId != Context.AccountId.Value) return Forbid();

            await _service.DispatchEventsAsync(Context, job);
            return Ok();
        }        

        // [HttpPost("filestorage")]
        // public async Task<IActionResult> FileStorageTestAsync([FromServices] IFileStorageService service, [FromBody] string contents, string filename, string bucket, string dstpath)
        // {            
        //     await service.UploadAsync(path, "plain/text", bucket, dstpath);
        //     await service.DownladAsync(bucket, dstpath, $"{path}.copy");

        //     return Ok();
        // }
    }
}