using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models.Billing;
using Services;

namespace Controllers
{
    [Route("/api/v1/[controller]")]
    [Authorize("default")]
    public class BillingController : APIController
    {
        private readonly IMapper _mapper;
        private readonly StatementService _service;
        private readonly IUserAdapter _userAdapter;
        private readonly MongoConnection _connection;

        public BillingController(
            IMapper mapper,
            StatementService service,
            IUserAdapter userAdapter,
            MongoConnection connection
            )
        {
            this._mapper = mapper;
            this._service = service;
            this._userAdapter = userAdapter;
            this._connection = connection;
        }

        [Authorize("admin")]
        [HttpGet("Organization({id})/Balance")]
        public async Task<EntityBalance> GetOrganizationBalanceAsync([FromRoute] Guid id)
        {
            var context = await _service.GetOrganizationOrThrowAsync(Context, id);
            return _mapper.Map<EntityBalance>(context.Billing);
        }

        [Authorize("admin")]
        [HttpPut("Organization({id})/AutoRefill")]
        [ProducesResponseType(typeof(EntityBalance), 200)]
        public async Task<IActionResult> SetOrganizationAutoRefillAsync([FromRoute] Guid id, [FromBody] AutoRefillRequest request)
        {
            if (request == null) return BadRequest();
            if (!request.MinBalance.HasValue)
            {
                request.AutoRefill = false;
                request.MaxBalance = null;
            }
            else
            {
                if (!request.MaxBalance.HasValue || request.MaxBalance.Value < request.MinBalance.Value) request.MaxBalance = request.MinBalance;
            }

            var entity = await _service.UpdateAutoRefillAsync(Context, id, request.AutoRefill, request.MinBalance, request.MaxBalance);
            if (entity == null) return NotFound();

            var result = _mapper.Map<EntityBalance>(entity);
            return Ok(result);
        }

        [Authorize("admin")]
        [HttpPost("Organization({id})/Adjustment")]
        [ProducesResponseType(typeof(EntityBalance), 200)]
        public async Task<IActionResult> AdjustOrganizationBalanceAsync([FromRoute] Guid id, [FromBody] BalanceAdjustment request)
        {
            if (string.IsNullOrEmpty(request?.Description) || string.IsNullOrEmpty(request?.Description) || request.Value == 0)
            {
                return BadRequest("Invalid or missing arguments");
            }

            var billingContext = await _service.GetOrganizationOrThrowAsync(Context, id);
            var user = await _userAdapter.GetByIdAsync(Context, Context.UserId.Value);

            var rndId = Guid.NewGuid();
            var adjustment = new Adjustment
            {
                Id = rndId,
                ExternalId = rndId.ToString(),
                AccountId = Context.AccountId.Value,
                OrganizationId = billingContext.Billing.Id,
                EntityId = billingContext.Billing.Id,
                Total = request.Value,
                Name = request.Description,
                Description = request.Notes,
                LastActor = Context.Actor,
                ReferenceDate = DateTime.UtcNow,
                LastModifiedOn = DateTime.UtcNow,

                AdjustedByEntityId = user.Id,
                AdjustedBy = user.Name,
            };

            await _service.AddAdjustmentAsync(Context, billingContext, adjustment);

            var result = _mapper.Map<EntityBalance>(billingContext.Billing);
            return Ok(result);
        }

        [Authorize("admin")]
        [HttpPost("Transaction({id})/Resolve")]
        [ProducesResponseType(typeof(TransactionResponse), 200)]
        public async Task<IActionResult> ResolveDisputeAsync([FromRoute] Guid id, [FromBody] ResolveDisputeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Notes) || string.IsNullOrWhiteSpace(request?.Description))
            {
                return BadRequest("Missing required fields");
            }

            var transaction = await _connection.Filter<BillTransaction>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Id, id)
                .FirstOrDefaultAsync();

            var dispute = transaction as Dispute;
            if (dispute == null)
            {
                switch (transaction)
                {
                    case Invoice _:
                        var disputes = await _connection.Filter<BillTransaction, Dispute>()
                            .Eq(x => x.AccountId, Context.AccountId)
                            .Eq(x => x.TransactionId, id)
                            .FindAsync();
                        if (disputes.Count != 1) return BadRequest("Can't determine dispute");
                        dispute = disputes[0];
                        break;

                    default:
                        return NotFound();
                }
            }

            if (dispute.ResolvedOn.HasValue) return BadRequest("Can't change resolution");

            var invoice = await _connection.Filter<BillTransaction, Invoice>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Id, dispute.TransactionId)
                .FirstOrDefaultAsync();

            if (invoice == null) throw new Exception("Original transaction not found");

            var billingContext = await _service.GetOrganizationOrThrowAsync(Context, dispute.OrganizationId.Value);
            var total = request.Resolution switch
            {
                DisputeResolution.Approve => invoice.Total, // todo: add support to partial credit? request.Value
                DisputeResolution.Reject => null,
                _ => throw new Exception("Invalid resolution")
            };

            var user = await _userAdapter.GetByIdAsync(Context, Context.UserId.Value);
            var rndId = Guid.NewGuid();
            var adjustment = new Adjustment()
            {
                Id = rndId,
                ExternalId = rndId.ToString(),
                AccountId = Context.AccountId.Value,
                OrganizationId = billingContext.Billing.Id,
                EntityId = billingContext.Billing.Id,
                Total = total.HasValue ? Math.Abs(total.Value) : default(decimal?),
                Name = request.Description,
                Description = request.Notes,
                LastActor = Context.Actor,
                ReferenceDate = DateTime.UtcNow,
                LastModifiedOn = DateTime.UtcNow,

                TransactionId = dispute.TransactionId,
                AdjustedByEntityId = user.Id,
                AdjustedBy = user.Name
            };

            adjustment = await _service.AddAdjustmentAsync(Context, billingContext, adjustment);
            dispute = await _service.ResolveDisputeAsync(Context, billingContext, user, dispute, request.Resolution, adjustment.Id);

            var result = new TransactionResponse
            {
                Invoice = invoice,
                Payment = null,
                Adjustment = adjustment,
                Dispute = dispute
            };

            return Ok(result);
        }

        [Authorize("managerplus")]
        [HttpPost("Transaction({id})/Dispute")]
        public async Task<Dispute> DisputeTransactionAsync([FromRoute] Guid id, [FromBody] DisputeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Reason) || string.IsNullOrWhiteSpace(request?.Description))
            {
                throw new BadRequestException("Missing required fields");
            }

            var invoice = await _connection.Filter<BillTransaction>()
                .OfType<BillTransaction, Invoice>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Id, id)
                .FirstOrDefaultAsync();

            if (invoice == null) throw new NotFoundException(nameof(BillTransaction), id);
            if (Context.Role != PI.Shared.Models.EntityRoleId.Admin && Context.OrganizationId.Value != invoice.OrganizationId)
            {
                throw new ForbiddenException(Context);
            }

            if (!invoice.Total.HasValue) throw new BadRequestException("Invoice doesn't have value");

            var billingContext = await _service.GetOrganizationOrThrowAsync(Context, invoice.OrganizationId.Value);
            var user = await _userAdapter.GetByIdAsync(Context, Context.UserId.Value);

            var rndId = Guid.NewGuid();
            var dispute = new Dispute
            {
                Id = rndId,
                ExternalId = rndId.ToString(),
                AccountId = Context.AccountId.Value,
                OrganizationId = billingContext.Billing.Id,
                EntityId = billingContext.Billing.Id,
                Total = null,
                Name = $"{invoice.Name}: {request.Reason}",
                Description = request.Description,
                LastActor = Context.Actor,
                ReferenceDate = DateTime.UtcNow,
                LastModifiedOn = DateTime.UtcNow,

                DisputedValue = Math.Abs(invoice.Total.Value),
                TransactionId = invoice.Id,
                InitiatedByEntityId = Context.UserId.Value,
                InitiatedBy = user.Name,
                ExtraMetadata = request.ExtraMetadata?.Count > 0 ? request.ExtraMetadata : null,
            };

            return await _service.AddDisputeAsync(Context, billingContext, dispute);
        }

        [Authorize("manager")]
        [HttpGet("Organization/Balance")]
        [ProducesResponseType(typeof(EntityBalance), 200)]
        public async Task<IActionResult> GetBalanceAsync([FromRoute] Guid id)
        {
            var entity = await _connection.Filter<BillEntity>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, id)
                .FirstOrDefaultAsync();

            if (entity == null) return NotFound();
            var result = _mapper.Map<EntityBalance>(entity);
            return Ok(result);
        }

        public class BalanceAdjustment
        {
            public decimal Value { get; set; }
            public string Description { get; set; }
            public string Notes { get; set; }
        }

        public class ResolveDisputeRequest
        {
            public decimal? Value { get; set; }
            public string Description { get; set; }
            public string Notes { get; set; }
            public DisputeResolution Resolution { get; set; }
        }

        public class AutoRefillRequest
        {
            public bool AutoRefill { get; set; }
            public decimal? MinBalance { get; set; }
            public decimal? MaxBalance { get; set; }
        }

        public class DisputeRequest
        {
            public string Reason { get; set; }
            public string Description { get; set; }
            public Dictionary<string, object> ExtraMetadata { get; set; }
        }

        public class EntityBalance
        {
            public Guid Id { get; set; }
            public Guid AccountId { get; set; }
            public string Name { get; set; }
            public DateTime CreatedOn { get; set; }
            public DateTime LastModifiedOn { get; set; }
            public decimal Balance { get; set; }
            public int TransactionNumber { get; set; }
            public decimal? PendingTotal { get; set; }
            public decimal? MinBalance { get; set; }
            public decimal? MaxBalance { get; set; }
            public bool AutoRefill { get; set; }
        }

        public class EntityBalanceProfile : Profile
        {
            public EntityBalanceProfile()
            {
                CreateMap<BillEntity, EntityBalance>();
            }
        }
    }
}
