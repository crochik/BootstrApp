using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Billing;
using PI.Shared.Models.Expressions;
using Services;

namespace Controllers
{
    [Route("/api/v1/[controller]")]
    public class StatementController : APIController
    {
        private readonly MongoConnection _connection;
        private readonly StatementService _service;

        public StatementController(
            MongoConnection connection,
            StatementService service
            )
        {
            this._connection = connection;
            this._service = service;
        }

        // [Authorize("admin")]
        // [HttpPost("Organization({id})/Reset")]
        // public async Task<IActionResult> ResetOrgAsync([FromRoute] Guid id)
        // {
        //     await _service.ResetAsync(Context, id);
        //     return Ok();
        // }

        // [Authorize("admin")]
        // [HttpPost("Organization({id})/Recalculate")]
        // public async Task<IActionResult> RecalculateForOrgAsync([FromRoute] Guid id)
        // {
        //     var date = new DateTime(2020, 07, 01);
        //     var tzi = TimeZoneInfo.FindSystemTimeZoneById("America/Phoenix");
        //     date = TimeZoneInfo.ConvertTime(date, tzi, TimeZoneInfo.Utc);

        //     var result = await _service.CalculateAsync(Context, id, date, 0);
        //     return Ok(result);
        // }

        [Authorize("admin")]
        [HttpPost("Recalculate")]
        public async Task<IActionResult> CalculateAsync()
        {
            var date = new DateTime(2020, 07, 01);
            var tzi = TimeZoneInfo.FindSystemTimeZoneById("America/Phoenix");
            date = TimeZoneInfo.ConvertTime(date, tzi, TimeZoneInfo.Utc);

            await _service.RecalculateAllAsync(Context, date);
            return Ok();
        }

        [Authorize("managerplus")]
        [HttpPost("Invoice/DataView")]
        [ProducesResponseType(typeof(DataViewResponse), 200)]
        public async Task<IActionResult> GetInvoicesDataViewAsync([FromBody] DataViewRequest request)
        {
            var query = _connection.Filter<BillTransaction>()
                .OfType<BillTransaction, Invoice>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .IncludeField(x => x.ReferenceDate)
                .IncludeField(x => x.Id)
                .IncludeField(x => x.Name)
                .IncludeField(x => x.Description)
                .IncludeField(x => x.Total)
                .SortAsc(x => x.Name)
                .SortDesc(x => x.CreatedOn);

            foreach (var criteria in request.Criteria)
            {
                switch (criteria.FieldName)
                {
                    case nameof(Invoice.ReferenceDate):
                        if (criteria.Operator != Operator.Gt) return BadRequest("Invalid Criteria");
                        if (!criteria.TryGetDate(out var date)) return BadRequest("Invalid Criteria");
                        query.Gt(x => x.ReferenceDate, date);
                        break;

                    case nameof(Invoice.Total):
                        if (!criteria.TryGetDecimal(out var value)) return BadRequest("Invalid Criteria");
                        switch (criteria.Operator)
                        {
                            case Operator.Gt:
                                query.Gt(x => x.Total, value.Value);
                                break;

                            case Operator.Lt:
                                query.Lt(x => x.Total, value.Value);
                                break;

                            default:
                                return BadRequest("Invalid Criteria");
                        }
                        break;

                    case nameof(Invoice.EntityId):
                        if (Context.Role == EntityRoleId.Admin)
                        {
                            if (criteria.Operator != Operator.Eq) return BadRequest("Invalid Criteria");
                            if (!criteria.TryGetUidValue(out var entityId)) return BadRequest("Invalid Criteria");
                            var organization = await _connection.Filter<Entity, PI.Shared.Models.Organization>()
                                .Eq(x => x.AccountId, Context.AccountId.Value)
                                .Eq(x => x.Id, entityId)
                                .FirstOrDefaultAsync();
                            if (organization == null) return Forbid();
                            query.Eq(x => x.EntityId, entityId);
                        }
                        break;

                    case nameof(Invoice.Id):
                        if (criteria.Operator != Operator.Ne) return BadRequest("Invalid Criteria");
                        if (!criteria.TryGetUidValue(out var id)) return BadRequest("Invalid Criteria");
                        query.Ne(x => x.Id, id);
                        break;

                    default:
                        return BadRequest($"Invalid criteria");
                }
            }

            if (Context.Role != EntityRoleId.Admin)
            {
                // limit to organization 
                query.Eq(x => x.EntityId, Context.OrganizationId.Value);
            }

            var response = new DataViewResponse
            {
                Request = request,
                Result = await query.FindAsync(),
                View = new DataView
                {
                    Name = "Invoices",
                    Fields = new FormField[] {
                        new TextField {Name = "id"},
                        new TextField {Name = "name"},
                        new TextField {Name = "description"},
                        new DateField {Name = "referenceDate"},
                    }
                }
            };

            return Ok(response.UpdateFields());
        }

        [Authorize("managerplus")]
        [HttpGet("Dispute/DataView")]
        [ProducesResponseType(typeof(DataViewResponse), 200)]
        public async Task<IActionResult> GetDisputesDataViewAsync()
        {
            await Task.CompletedTask;

            // ...
            return Ok();
        }

        [Authorize("managerplus")]
        [HttpGet("Transaction({id})")]
        [ProducesResponseType(typeof(TransactionResponse), 200)]
        public async Task<IActionResult> GetTransactionByIdAsync([FromRoute] Guid id, [FromServices] MongoConnection connection)
        {
            var query = connection.Filter<BillTransaction>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, id);

            if (Context.Role != PI.Shared.Models.EntityRoleId.Admin) query.Eq(x => x.OrganizationId, Context.OrganizationId.Value);

            var transaction = await query.FirstOrDefaultAsync();
            if (transaction == null) return NotFound();

            var result = new TransactionResponse
            {
                Invoice = transaction as Invoice,
                Payment = transaction as Payment,
                Adjustment = transaction as Adjustment,
                Dispute = transaction as Dispute,
            };

            switch (transaction)
            {
                case Invoice invoice:
                    // last dispute
                    result.Dispute = await connection.Filter<BillTransaction, Dispute>()
                        .Eq(x => x.AccountId, invoice.AccountId)
                        .Eq(x => x.TransactionId, invoice.Id)
                        .SortDesc(x => x.CreatedOn)
                        .Limit(1)
                        .FirstOrDefaultAsync();
                    break;

                case Dispute dispute:
                    result.Invoice = await connection.Filter<BillTransaction, Invoice>()
                        .Eq(x => x.AccountId, dispute.AccountId)
                        .Eq(x => x.Id, dispute.TransactionId)
                        .FirstOrDefaultAsync();
                    break;

                case Adjustment adjustment:
                    if (adjustment.TransactionId.HasValue)
                    {
                        result.Invoice = await connection.Filter<BillTransaction, Invoice>()
                            .Eq(x => x.AccountId, adjustment.AccountId)
                            .Eq(x => x.Id, adjustment.TransactionId)
                            .FirstOrDefaultAsync();

                        result.Dispute = await connection.Filter<BillTransaction, Dispute>()
                            .Eq(x => x.AccountId, adjustment.AccountId)
                            .Eq(x => x.AdjustmentId, adjustment.Id)
                            .SortDesc(x => x.CreatedOn)
                            .Limit(1)
                            .FirstOrDefaultAsync();
                    }
                    break;
            }

            if (result.Dispute != null && result.Dispute.AdjustmentId.HasValue && result.Adjustment == null)
            {
                // look for adjustmentt
                result.Adjustment = await connection.Filter<BillTransaction, Adjustment>()
                    .Eq(x => x.AccountId, result.Dispute.AccountId)
                    .Eq(x => x.Id, result.Dispute.AdjustmentId.Value)
                    .FirstOrDefaultAsync();
            }

            return Ok(result);
        }
    }

    public class TransactionResponse
    {
        public Invoice Invoice { get; set; }
        public Payment Payment { get; set; }
        public Adjustment Adjustment { get; set; }
        public Dispute Dispute { get; set; }
    }
}
