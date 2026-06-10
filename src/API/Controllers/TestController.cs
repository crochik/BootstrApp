using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Services;

namespace Controllers;

[Route("[controller]")]
public class TestController : APIController
{
    // [BsonCollection("Lead.Snapshot.FirstAppt")]
    // public class LeadSnapshotFirstAppt
    // {
    //     [BsonId]
    //     public Guid Id { get; set; }
    //     public Guid AppointmentId { get; set; }
    //     public DateTime CreatedOn { get; set; }
    //     public Actor LastActor { get; set; }
    // }

    // [AllowAnonymous]
    // [HttpGet("LeadSnapshotFirstAppt")]
    // public async Task<IActionResult> DipperTestAsync([FromServices] MongoConnection connection)
    // {
    //     var today = DateTime.UtcNow;
    //     var list = await connection.Filter<LeadSnapshotFirstAppt>()
    //         .Gte(x => x.CreatedOn, today.AddDays(-30))
    //         .DipperAsync<BsonDocument>("fci.Report.PPA2");

    //     return Ok(list);
    // }
    
    [Authorize("admin")]
    [HttpGet("Abort")]
    public IActionResult StopApplication([FromServices] IHostApplicationLifetime lifetime)
    {
        Task.Run(async () =>
        {
            await Task.Delay(1);
            lifetime.StopApplication();
        });

        return Ok();
    }

    [Authorize]
    [HttpGet("User")]
    public IActionResult Users()
    {
        var isUser = User.HasClaim((claim) => claim.Type == "sub");
        System.Console.WriteLine($"{User.Identity.Name}: {isUser}");
        return new JsonResult(from c in User.Claims select new { c.Type, c.Value });
    }

    [Authorize("partner")]
    [HttpGet("Partner")]
    public IActionResult PartnerAsync()
    {
        return Ok(Context);
    }

    [HttpPost("Protect")]
    public IActionResult AddSecret(string value, [FromServices] IDataProtectionProvider provider)
    {
        var protector = provider.CreateProtector(nameof(TestController));
        var secret = protector.Protect(value);
        return Ok(secret);
    }

    [HttpPost("Unprotect")]
    public IActionResult UnprotectSecret(string value, [FromServices] IDataProtectionProvider provider)
    {
        var protector = provider.CreateProtector(nameof(TestController));
        var plain = protector.Unprotect(value);
        return Ok(plain);
    }

    [Authorize("admin")]
    [HttpGet("Scheduler/Replay")]
    public async Task<IActionResult> ReplaySchedulingSessionAsync(
        [FromQuery] Guid sessionId,
        [FromServices] MongoConnection connection,
        [FromServices] AppointmentSchedulerService service)
    {
        var session = await connection.Filter<PI.Shared.Models.SchedulerSession>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, sessionId)
            .FirstOrDefaultAsync();

        var eventsAndSlots = await service.GetEventsAndSlotsAsync(Context, session, null, null);
        var result = await service.TestCreateAppointmentAsync(Context, session, eventsAndSlots);

        return Ok(result);
    }
}
