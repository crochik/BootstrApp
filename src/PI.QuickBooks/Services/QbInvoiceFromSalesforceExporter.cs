using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Intuit.Ipp.Data;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.QuickBooks.Models;
using PI.Shared.Models;
using PI.Shared.Salesforce;
using PI.Shared.Salesforce.Models;
using Task = System.Threading.Tasks.Task;

namespace PI.QuickBooks.Services;

/// <summary>
/// rewrite of QbTransactionExporter using generic implementation
/// NOT IN USE RIGHT NOW 
/// </summary>
public class QbInvoiceFromSalesforceExporter(
    ILogger<QbInvoiceFromSalesforceExporter> logger,
    MongoConnection connection,
    QuickBooksService service,
    OptionLoader loader)
    : AbstractQbInvoiceExporter(logger, connection, service)
{
    public override Lead Lead => _option.Lead;
    public override SfWorkOrder WorkOrder => _option?.WorkOrder;
    public override string ProposalNumber => _option?.Option.ProposalNumber;
    public override string ProposalName => _option?.Option.Name;
    public override string ProposalNotes => _option?.Option.Notes;

    private LoadedOption _option;

    private async Task InitAsync(IEntityContext context, Guid optionId)
    {
        var option = await loader.LoadOptionAsync(context, optionId);

        if (!option.LeadId.HasValue)
        {
            Errors.Add("Couldn't determine the Lead");
            return;
        }

        option.Lead ??= await connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, option.LeadId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var entity = await connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, option.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        _context = entity.Context;
        _option = option;

        _localCache = new LocalCache();
        await service.LoadAccountsAsync(_context, _localCache);
        await service.SyncAccountsAsync(_context, _localCache);
    }

    public async Task<Result<QbEntity>> ExportSalesforceInvoiceAsync(IEntityContext context, Guid optionId)
    {
        await InitAsync(context, optionId);

        if (_option == null || _context == null)
        {
            Errors.Add("Failed to initialize");
            return Result.Error<QbEntity>("Failed to initialize");
        }

        return await ExportAsync();
    }

    private async Task<Result<QbEntity>> ExportAsync()
    {
        var result = await ExportAllAsync();
        if (!result.IsSuccess) return result.ConvertTo<QbEntity>();

        var qbEntity = await service.SaveAsync(_context, result.Value, SfOption.ObjectTypeName, _option.Id, result.Value.DocNumber, result.Value.Id);

        // add signed proposal
        var attachment = await AttachProposalPdfToInvoiceAsync(qbEntity);
        if (attachment.IsError)
        {
            Errors.Add(attachment.Status);
        }

        return Result.Success(qbEntity);
    }

    private async Task<Result<Attachable>> AttachProposalPdfToInvoiceAsync(QbEntity qbEntity)
    {
        // assume just one will be found
        var pdf = _option.ExternalLinks?.FirstOrDefault(x => x.Type == "Proposal PDF");
        if (pdf == null)
        {
            // no pdf
            return Result.Unknown<Attachable>("no proposal pdf link");
        }

        return await service.AttachAsync(
            _context,
            _localCache,
            pdf.Url,
            $"{pdf.Name}.pdf",
            new[]
            {
                new AttachableRef
                {
                    EntityRef = new ReferenceType
                    {
                        type = qbEntity.EntityType,
                        Value = qbEntity.ExternalId,
                    }
                }
            }
        );
    }

    protected override async Task<Invoice> BuildInvoiceAsync()
    {
        _items = _option.SectionLineItems?
            .Select((x) => new GenericLineItem
                {
                    ItemKey = x.Product,
                    Description = x.Description,
                    Name = x.Name,
                    TotalCost = x.TotalCost,
                    TotalBeforeTax = x.TotalBeforeTax,
                    AdjustedQuantity = x.AdjustedQuantity ?? x.Quantity,
                    // Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice,
                    TotalPrice = x.TotalPrice,
                    // TaxPrice = x.TaxPrice,
                    TaxGroup = x.TaxGroup,
                    IsTaxable = x.TaxFactor > 0,
                }
            )
            .ToArray() ?? [];

        if (_option.OptionLineItems?.Length > 0)
        {
            _items = _items
                .Concat(_option.OptionLineItems
                    .Select(x =>
                        new GenericLineItem
                        {
                            ItemKey = x.Product,
                            Description = x.Description,
                            Name = x.Name,
                            TotalCost = x.TotalCost,
                            TotalBeforeTax = x.TotalBeforeTax,
                            AdjustedQuantity = x.AdjustedQuantity ?? x.Quantity,
                            // Quantity = x.Quantity,
                            UnitPrice = x.UnitPrice,
                            TotalPrice = x.TotalPrice,
                            // TaxPrice = x.TaxPrice,
                            TaxGroup = x.TaxGroup,
                            IsTaxable = x.TaxFactor > 0,
                        }
                    ))
                .ToArray();
        }

        GroupItems();
        
        _catalogItems = (await _connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, _context.AccountId)
            .Eq(x => x.EntityId, _context.EntityId)
            .In(x => x.Salesforce.Product2, _items.Select(x => x.ItemKey))
            .Ne(x => x.IsActive, false)
            .FindAsync()).ToDictionary(x => x.Salesforce.Product2);

        return await base.BuildInvoiceAsync();
    }
}