// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using Crochik.Mongo;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Logging;
// using PI.Shared.Constants;
// using PI.Shared.Controllers;
// using PI.Shared.Data.Adapters;
// using PI.Shared.Models;
//
// namespace Controllers;
//
// [Authorize("default")]
// [Produces("application/json")]
// [Route("/api/v1/[controller]")]
// public class IntegrationController : AbstractIntegrationController
// {
//     public IntegrationController(
//         ILogger<IntegrationController> logger,
//         IIntegrationAdapter integrationAdapter,
//         ILeadTypeAdapter leadTypeAdapter,
//         IEntityIntegrationAdapter entityIntegrationAdapter,
//         ILeadTypeIntegrationAdapter leadTypeIntegrationAdapter,
//         IAppointmentTypeAdapter appointmentTypeAdapter,
//         IAppointmentTypeIntegrationAdapter appointmentTypeIntegrationAdapter
//     ) : base(
//         logger,
//         integrationAdapter,
//         leadTypeAdapter,
//         entityIntegrationAdapter,
//         leadTypeIntegrationAdapter,
//         appointmentTypeAdapter,
//         appointmentTypeIntegrationAdapter            
//     )
//     {
//     }
//
//     [AllowAnonymous]
//     [HttpGet]
//     [ProducesResponseType(typeof(IEnumerable<AppIntegration>), 200)]
//     public async Task<IActionResult> GetAllAsync()
//     {
//         var list = await _integrationAdapter.GetAllAsync();
//         return Ok(list);
//     }
//
//     [Authorize("default")]
//     [HttpGet("/api/v1/[controller][[Selected]]")]
//     [ProducesResponseType(typeof(IEnumerable<AppIntegration>), 200)]
//     public async Task<IActionResult> GetSelectedIntegrationsAsync()
//     {
//         var result = await _entityIntegrationAdapter.GetAsync(Context);
//         return Ok(result);
//     }
//
//     [Authorize("managerplus")]
//     [HttpGet("/api/v1/Entity({id})/[controller]")]
//     [ProducesResponseType(typeof(IEnumerable<AppIntegration>), 200)]
//     public async Task<IActionResult> GetIntegrationsForEntityAsync(
//         [FromRoute] Guid id,
//         [FromServices] IEntityIdentityAdapter entityAdapter
//     )
//     {
//         var entity = await entityAdapter.GetEntityByIdAsync(id);
//         if (entity == null) return NotFound();
//         if (!Context.CanAccess(entity)) return Forbid();
//
//         var result = await _entityIntegrationAdapter.GetAsync(entity.Context);
//         return Ok(result);
//     }
//
//     [Authorize("manager")]
//     [HttpGet("/api/v1/User/[controller]")]
//     public async Task<IEnumerable<AppIntegration>> GetEntityIntegrationsAsync()
//     {
//         var result = await _entityIntegrationAdapter.GetAsync(Context);
//
//         return result != null ?
//             result.Select(x => _integrationAdapter.GetById(x.IntegrationId)) :
//             Array.Empty<AppIntegration>();
//     }
//
//     [Authorize("manager")]
//     [HttpGet("/api/v1/Organization/[controller]")]
//     public async Task<IEnumerable<AppIntegration>> GetOrganizationIntegrationsAsync()
//     {
//         var orgContext = new OrganizationContext(Context.OrganizationId.Value, Context.AccountId);
//         var result = await _entityIntegrationAdapter.GetAsync(orgContext);
//
//         return result != null ?
//             result.Select(x => _integrationAdapter.GetById(x.IntegrationId)) :
//             Array.Empty<AppIntegration>();
//     }
//
//     [Authorize("manager")]
//     [HttpGet("/api/v1/AppointmentType({id})/[controller]")]
//     public async Task<IEnumerable<AppIntegration>> GetAppointmentTypeIntegrationsAsync([FromRoute] Guid id)
//     {
//         // TODO: enforce access rules
//         // ... 
//         var result = await _appointmentTypeIntegrationAdapter.GetAsync(id);
//
//         return result != null ?
//             result.Select(x => _integrationAdapter.GetById(x.IntegrationId)) :
//             Array.Empty<AppIntegration>();
//     }
//
//     [Authorize("managerplus")]
//     [HttpGet("/api/v1/LeadType({id})/[controller]")]
//     public async Task<IEnumerable<AppIntegration>> GetLeadTypeIntegrationsAsync([FromRoute] Guid id)
//     {
//         // TODO: enforce access rules
//         // ... 
//         var result = await _leadTypeIntegrationAdapter.GetAsync(id);
//
//         return result != null ?
//             result.Select(x => _integrationAdapter.GetById(x.IntegrationId)) :
//             Array.Empty<AppIntegration>();
//     }
//
//
//     [Authorize("managerplus")]
//     [HttpDelete("/api/v1/AppointmentType({id})/[controller]({serviceName})")]
//     public async Task<IActionResult> RemoveAppointmentTypeIntegrationAsync(Guid id, string serviceName)
//     {
//         // TODO: enforce access rules
//         // ...
//
//         var result = await _appointmentTypeIntegrationAdapter.DeleteAsync(id, serviceName);
//         return result ? (IActionResult)Ok() : NotFound();
//     }
//
//     [Authorize("manager")]
//     [HttpDelete("/api/v1/LeadType({id})/[controller]({serviceName})")]
//     public async Task<IActionResult> RemoveLeadTypeIntegrationAsync(Guid id, string serviceName)
//     {
//         // TODO: enforce access rules
//         // ...
//
//         var result = await _leadTypeIntegrationAdapter.DeleteAsync(id, serviceName);
//         return result ? (IActionResult)Ok() : NotFound();
//     }
//
//     [Authorize("admin")]
//     [HttpPost("Salesforce/UpdateUrls")]
//     public async Task<IActionResult> UpdateUrlsAsync(
//         [FromServices] IAccountAdapter accountAdapter,
//         [FromServices] MongoConnection connection
//     )
//     {
//         var account = await accountAdapter.GetByIdAsync(Context.AccountId.Value);
//         var identity = account.FirstIdentity(ExternalProvider.Salesforce.ToString());
//         var baseUrl = identity?.ExternalIdentity?.Token is PI.Shared.Salesforce.Models.SalesforceToken sf ?
//             sf.InstanceUrl :
//             null;
//
//         if (string.IsNullOrEmpty(baseUrl)) return NotFound();
//
//         // long count = await UpdateAppointmentsAsync(connection, baseUrl);
//         long count = await UpdateLeadsAsync(connection, baseUrl);
//
//         return Ok(count);
//     }
//
//     private async Task<long> UpdateAppointmentsAsync(MongoConnection connection, string baseUrl)
//     {
//         var cursor = connection.Filter<Appointment>()
//             .Eq(x => x.AccountId, Context.AccountId.Value)
//             .ElemMatchBuilder(
//                 x => x.Integrations,
//                 q => q
//                     .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
//                     .Exists(x => x.Url, false)
//             ).ToCursor();
//
//         var count = 0L;
//         while (await cursor.MoveNextAsync())
//         {
//             var batch = new List<MongoDB.Driver.UpdateOneModel<Appointment>>();
//             foreach (var row in cursor.Current)
//             {
//                 var missing = row.Integrations.Where(i =>
//                     i.IntegrationId == IntegrationIds.Salesforce &&
//                     string.IsNullOrEmpty(i.Url));
//
//                 foreach (var i in missing)
//                 {
//                     var url = $"{baseUrl}/{i.ExternalId}";
//
//                     batch.Add(connection.Filter<Appointment>()
//                         .Eq(x => x.Id, row.Id)
//                         .ElemMatchBuilder(
//                             x => x.Integrations,
//                             q => q
//                                 .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
//                                 .Eq(x => x.ExternalId, i.ExternalId)
//                                 .Exists(x => x.Url, false)
//                         )
//                         .Update
//                         .Set($"Integrations.$.Url", url)
//                         .UpdateOneModel()
//                     );
//                 }
//             }
//
//             if (batch.Count > 0)
//             {
//                 var result = await connection.BulkWriteAsync(batch);
//                 count += result.ModifiedCount;
//                 batch.Clear();
//             }
//         }
//
//         return count;
//     }
//
//     private async Task<long> UpdateLeadsAsync(MongoConnection connection, string baseUrl)
//     {
//         var cursor = connection.Filter<Lead>()
//             .Eq(x => x.AccountId, Context.AccountId.Value)
//             .ElemMatchBuilder(
//                 x => x.Integrations,
//                 q => q
//                     .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
//                     .Exists(x => x.Url, false)
//             ).ToCursor();
//
//         var count = 0L;
//         while (await cursor.MoveNextAsync())
//         {
//             var batch = new List<MongoDB.Driver.UpdateOneModel<Lead>>();
//             foreach (var row in cursor.Current)
//             {
//                 var missing = row.Integrations.Where(i =>
//                     i.IntegrationId == IntegrationIds.Salesforce &&
//                     string.IsNullOrEmpty(i.Url));
//
//                 foreach (var i in missing)
//                 {
//                     var url = $"{baseUrl}/{i.ExternalId}";
//
//                     batch.Add(connection.Filter<Lead>()
//                         .Eq(x => x.Id, row.Id)
//                         .ElemMatchBuilder(
//                             x => x.Integrations,
//                             q => q
//                                 .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
//                                 .Eq(x => x.ExternalId, i.ExternalId)
//                                 .Exists(x => x.Url, false)
//                         )
//                         .Update
//                         .Set($"Integrations.$.Url", url)
//                         .UpdateOneModel()
//                     );
//                 }
//             }
//
//             if (batch.Count > 0)
//             {
//                 var result = await connection.BulkWriteAsync(batch);
//                 count += result.ModifiedCount;
//                 batch.Clear();
//             }
//         }
//
//         return count;
//     }
// }