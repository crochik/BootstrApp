using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.QuickBooks.Models;
using PI.Shared.Models;
using PI.Shared.Salesforce.Models;
using Estimate = PI.ProductCatalog.Models.Estimate;
using Invoice = Intuit.Ipp.Data.Invoice;

namespace PI.QuickBooks.Services;

public class QbInvoiceFromEstimateExporter(
    ILogger<QbInvoiceFromEstimateExporter> logger,
    MongoConnection connection,
    QuickBooksService service
) : AbstractQbInvoiceExporter(logger, connection, service)
{
    private Estimate Estimate { get; set; }

    private Lead _lead;
    public override Lead Lead => _lead;

    private SalesforceWorkOrderObject _workOrder;
    public override SfWorkOrder WorkOrder => _workOrder?.Properties;
    public override string ProposalNumber => Estimate?.EstimateNumber;
    public override string ProposalName => Estimate?.Name;
    public override string ProposalNotes => Estimate?.Description;

    public async Task<Result<QbEntity>> ExportInvoiceAsync(IEntityContext context, Guid id)
    {
        var estimate = await _connection.Filter<Estimate>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (estimate == null) return Result.Error<QbEntity>("Estimate not found");
        return await ExportInvoiceAsync(context, estimate);
    }

    public async Task<Result<QbEntity>> ExportInvoiceAsync(IEntityContext context, Estimate estimate)
    {
        Estimate = estimate;

        _workOrder = await _connection.Filter<SalesforceWorkOrderObject>("salesforce.WorkOrder")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.ExternalId, estimate.ProjectExternalId)
            .Ne(x => x.Properties.IsDeleted, true)
            .FirstOrDefaultAsync();

        if (_workOrder == null) return Result.Error<QbEntity>("WorkOrder not found");
        if (!_workOrder.LeadId.HasValue) return Result.Error<QbEntity>("Project not assigned to a Lead");

        _lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, _workOrder.LeadId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (_lead == null)
        {
            Errors.Add($"Lead {_workOrder.LeadId} not found or has been deactivated");
            return Result.Error<QbEntity>($"Lead {_workOrder.LeadId} not found or has been deactivated");
        }

        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, Lead.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        _context = entity.Context;
        _localCache = new LocalCache();

        await _service.LoadAccountsAsync(_context, _localCache);
        await _service.SyncAccountsAsync(_context, _localCache);

        return await ExportAsync(estimate);
    }

    private async Task<Result<QbEntity>> ExportAsync(Estimate estimate)
    {
        var result = await ExportAllAsync();
        if (!result.IsSuccess) return result.ConvertTo<QbEntity>();

        var qbEntity = await _service.SaveAsync(_context, result.Value, Estimate.ObjectTypeFullName, estimate.Id, result.Value.DocNumber, result.Value.Id);

        // TODO: add signed proposal
        // var attachment = await AttachProposalPdfToInvoiceAsync(qbEntity);
        // if (attachment.IsError)
        // {
        //     Errors.Add(attachment.Status);
        // }

        return Result.Success(qbEntity);
    }

    protected override async Task<Invoice> BuildInvoiceAsync()
    {
        // TODO: probably can/should also have flagged at the customer/project or invoice level 
        // ...
        var taxable = Estimate.IsNonTaxable ? [] : Estimate.TaxRates?.TaxLiabilities?.Where(x => x.Amount > 0).Select(x => x.Category).ToHashSet() ?? [];

        _items = Estimate.LineItems.Select(x => new GenericLineItem
        {
            ItemKey = x.ItemId.ToString(),
            Description = x.Description,
            Name = x.Name,
            TotalCost = x.TotalCost,
            TotalBeforeTax = x.TotalPrice,
            AdjustedQuantity = x.AdjustedQuantity?.Units ?? x.Quantity?.Units,
            // Quantity = x.Quantity?.Units,
            UnitPrice = x.UnitPrice,
            TotalPrice = x.TotalPrice,
            // TaxPrice = x.TaxPrice,
            TaxGroup = x.TaxCategory?.ToString(),
            IsTaxable = !x.IsNonTaxable && x.TaxCategory.HasValue && taxable.Contains(x.TaxCategory.Value),
        }).ToArray();

        GroupItems();

        _catalogItems = (
            await _connection.Filter<CatalogItem>()
                .Eq(x => x.AccountId, _context.AccountId)
                .Eq(x => x.EntityId, _context.EntityId)
                // .In(x => x.Salesforce.Product2, _items.Select(x => x.ItemKey))
                .In(x => x.Id, Estimate.LineItems.Select(x => x.ItemId))
                .Ne(x => x.IsActive, false)
                .FindAsync()
        ).ToDictionary(x => x.Id.ToString());

        return await base.BuildInvoiceAsync();
    }

    // private async Task<Result<Attachable>> AttachProposalPdfToInvoiceAsync(QbEntity qbEntity)
    // {
    //     // assume just one will be found
    //     var pdf = _option.ExternalLinks?.FirstOrDefault(x => x.Type == "Proposal PDF");
    //     if (pdf == null)
    //     {
    //         // no pdf
    //         return Result.Unknown<Attachable>("no proposal pdf link");
    //     }
    //
    //     return await _service.AttachAsync(
    //         _context,
    //         _localCache,
    //         pdf.Url,
    //         $"{pdf.Name}.pdf",
    //         new[]
    //         {
    //             new AttachableRef
    //             {
    //                 EntityRef = new ReferenceType
    //                 {
    //                     type = qbEntity.EntityType,
    //                     Value = qbEntity.ExternalId,
    //                 }
    //             }
    //         }
    //     );
    // }
}