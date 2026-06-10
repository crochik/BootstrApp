using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Intuit.Ipp.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.ProductCatalog.Models;
using PI.QuickBooks.Models;
using PI.Shared.Models;
using PI.Shared.Salesforce;
using PI.Shared.Salesforce.Models;
using EmailAddress = Intuit.Ipp.Data.EmailAddress;
using Task = System.Threading.Tasks.Task;

namespace PI.QuickBooks.Services;

public class QbTransactionExporter
{
    private const bool UseBeforeTaxPrice = true;

    private readonly ILogger<QbTransactionExporter> _logger;
    private readonly MongoConnection _connection;
    private readonly QuickBooksService _service;
    private readonly OptionLoader _loader;

    private AbstractSfLineItem[] _items;
    private LoadedOption _option;
    private IEntityContext _context;
    private QbEntity _project;
    private QbEntity _customer;
    private Dictionary<string, CatalogItem> _catalogItems;
    private Dictionary<Guid, QbEntity> _qbItems;

    private LocalCache _localCache;

    private Invoice Invoice { get; set; }
    public List<string> Errors { get; } = new();

    public QbTransactionExporter(
        ILogger<QbTransactionExporter> logger,
        MongoConnection connection,
        QuickBooksService service,
        OptionLoader loader
    )
    {
        _logger = logger;
        _connection = connection;
        _service = service;
        _loader = loader;
    }

    private async Task InitAsync(IEntityContext context, Guid optionId)
    {
        var option = await _loader.LoadOptionAsync(context, optionId);

        if (!option.LeadId.HasValue)
        {
            Errors.Add("Couldn't determine the Lead");
            return;
        }

        option.Lead ??= await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, option.LeadId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, option.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        _context = entity.Context;
        _option = option;

        _localCache = new LocalCache();
        await _service.LoadAccountsAsync(_context, _localCache);
        await _service.SyncAccountsAsync(_context, _localCache);
    }

    public async Task<Result<QbEntity>> ExportInvoiceAsync(IEntityContext context, Guid optionId)
    {
        await InitAsync(context, optionId);

        if (_option == null || _context == null)
        {
            Errors.Add("Failed to initialize");
            return Result.Error<QbEntity>("Failed to initialize");
        }

        // customer
        if (_customer == null)
        {
            if (_option.Lead == null)
            {
                Errors.Add($"Lead {_option.LeadId} not found or has been deactivated");
                return Result.Error<QbEntity>($"Lead {_option.LeadId} not found or has been deactivated");
            }
            
            var customerResult = await _service.GetOrCreateAsync(_context, _option.Lead);
            if (!customerResult.IsSuccess)
            {
                Errors.Add($"Failed to Create Customer: {customerResult.Status}");
                return customerResult;
            }

            _customer = customerResult.Value;
        }

        // project
        if (_project == null)
        {
            var projectResult = await _service.GetOrCreateAsync(_context, _customer, _option.WorkOrder, _option.Lead);
            if (!projectResult.IsSuccess)
            {
                Errors.Add($"Failed to Create Project: {projectResult.Status}");
                return projectResult;
            }

            _project = projectResult.Value;
        }


        Preferences = (await _service.GetAllAsync<Preferences>(_context)).FirstOrDefault();

        Invoice = await BuildInvoiceAsync();

        var result = await _service.ExportAsync(_context, Invoice);
        if (!result.IsSuccess)
        {
            Errors.Add($"Failed to Export Invoice: {result.Status}");
            if (result.Status.Contains("Duplicate Document Number Error"))
            {
                return Result<QbEntity>.Unknown($"There is already an invoice in QuickBooks for {Invoice.DocNumber}");
            }

            return result.ConvertTo<QbEntity>();
        }

        var qbEntity = await _service.SaveAsync(_context, result.Value, SfOption.ObjectTypeName, _option.Id, result.Value.DocNumber, result.Value.Id);

        // add signed proposal
        var attachment = await AttachProposalPdfToInvoiceAsync(qbEntity);
        if (attachment.IsError)
        {
            Errors.Add(attachment.Status);
        }

        return Result.Success(qbEntity);
    }

    public Preferences Preferences { get; private set; }

    private async Task<Result<Attachable>> AttachProposalPdfToInvoiceAsync(QbEntity qbEntity)
    {
        // assume just one will be found
        var pdf = _option.ExternalLinks?.FirstOrDefault(x => x.Type == "Proposal PDF");
        if (pdf == null)
        {
            // no pdf
            return Result.Unknown<Attachable>("no proposal pdf link");
        }

        return await _service.AttachAsync(
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

    private async Task<Invoice> BuildInvoiceAsync()
    {
        _items = _option.SectionLineItems?.Select(AbstractSfLineItem (x) => x).ToArray() ?? [];
        if (_option.OptionLineItems?.Length > 0) _items = _items.Concat(_option.OptionLineItems).ToArray();

        GroupItems();

        _catalogItems = (await _connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, _option.AccountId)
            .Eq(x => x.EntityId, _option.EntityId)
            .In(x => x.Salesforce.Product2, _items.Select(x => x.Product))
            .Ne(x => x.IsActive, false)
            .FindAsync()).ToDictionary(x => x.Salesforce.Product2);

        _qbItems = (await _connection.Filter<QbEntity>()
            .Eq(x => x.AccountId, _option.AccountId)
            .Eq(x => x.EntityId, _option.EntityId)
            .Eq(x => x.EntityType, nameof(Item))
            .In($"{nameof(QbEntity.Refs)}.{nameof(CatalogItem)}", _catalogItems.Values.Select(x => x.Id))
            .FindAsync()).ToDictionary(x => ((ObjectId)x.Refs[nameof(CatalogItem)]).ToGuid());

        foreach (var item in _catalogItems.Values)
        {
            if (_qbItems.ContainsKey(item.Id)) continue;

            var itemResult = await _service.GetOrCreateAsync(_context, _localCache, item);
            if (itemResult.IsSuccess)
            {
                _qbItems.Add(item.Id, itemResult.Value);
                continue;
            }

            Errors.Add(itemResult.Status);
        }

        var otherItems = new Dictionary<string, string>();
        foreach (var item in _items)
        {
            if (_catalogItems.ContainsKey(item.Product)) continue;

            // TODO: try to find in QuickBooks
            var existing = await _service.FindAsync<Item>(_context, nameof(Item.Name), item.Name);
            if (existing.Count == 1)
            {
                var qbItem = existing.First();
                _logger.LogInformation("Found {DisplayName} with {Id}", qbItem.FullyQualifiedName, qbItem.Id);
                otherItems[item.Name] = qbItem.Id;
                Errors.Add($"Item {item.Product} not in Price book but exists in QB: {item.Name} #{qbItem.Id}");
                continue;
            }

            Errors.Add($"Didn't find {item.Name}: {item.Product} in Price book (added as service)");
        }

#if DEBUG
        var proposalNumber = $"{_option.Option.ProposalNumber}.{DateTime.UtcNow:MMddHHmm}";
#else
        var proposalNumber = _option.Option.ProposalNumber ?? $"{DateTime.UtcNow:MMddHHmm}";
#endif

        Dictionary<string, TaxCode> taxGroupsMap;

        if (Preferences?.TaxPrefs.PartnerTaxEnabled ?? false)
        {
            // using auto tax, no mapping
            taxGroupsMap = new Dictionary<string, TaxCode>();
        }
        else
        {
            // load tax groups so it can try to match automatically
            var taxGroups = await _service.GetAllAsync<TaxCode>(_context);
            taxGroupsMap = taxGroups
                .Where(x => x.Active) // x.TaxCodeConfigType != GTMConfigTypeEnum.SYSTEM_GENERATED &&
                .DistinctBy(x => x.Name)
                .ToDictionary(x => x.Name);

            if (taxGroupsMap.TryGetValue("Exempt", out var exempt))
            {
                taxGroupsMap.TryAdd("Non Taxable Discount", exempt);
            }

            var sfTaxGroupCodes = _items.Select(x => x.TaxGroup).Distinct();
            var sfTaxGroups = await _connection.Filter<SalesforceObject<SfTaxGroup>>("salesforce.INET_TaxGroup__c")
                .Eq(x => x.AccountId, _context.AccountId.Value)
                .In(x => x.ExternalId, sfTaxGroupCodes)
                .Ne(x => x.Properties.IsDeleted, true)
                .FindAsync();
            foreach (var taxGroup in sfTaxGroups)
            {
                if (taxGroupsMap.TryGetValue(taxGroup.Properties.Name, out var existingTaxGroup))
                {
                    taxGroupsMap.Add(taxGroup.ExternalId, existingTaxGroup);
                }
                else
                {
                    _logger.LogInformation("Didn't find {TaxGroup} in QuickBooks: ({SfTaxGroupId})", taxGroup.Properties.Name, taxGroup.ExternalId);
                }
            }
        }

        var invoice = new Invoice
        {
            // Estimate
            // ExpirationDate = DateTime.UtcNow.AddDays(30),
            // ExpirationDateSpecified = true,
            CustomerRef = new ReferenceType
            {
                Value = _project.ExternalId,
            },

            // can't create projects using API so this would have to be conditional
            // ...
            // ProjectRef = new ReferenceType
            // {
            //     Value = projectId,
            // },
            CustomerMemo = new MemoRef
            {
                Value = $"{_option.Option.ProposalNumber}: {_option.Option.Name}\n{_option.Option.Notes}",
            },
            BillAddr = new PhysicalAddress
            {
                Line1 = _option.WorkOrder.Street,
                City = _option.WorkOrder.City,
                CountrySubDivisionCode = _option.WorkOrder.State,
                PostalCode = _option.WorkOrder.PostalCode,
                Country = _option.WorkOrder.Country,
            },
            ShipAddr = new PhysicalAddress
            {
                Line1 = _option.WorkOrder.Street,
                City = _option.WorkOrder.City,
                CountrySubDivisionCode = _option.WorkOrder.State,
                PostalCode = _option.WorkOrder.PostalCode,
                Country = _option.WorkOrder.Country,
            },
            BillEmail = new EmailAddress
            {
                Address = _option.Lead.NormalizedEmail,
            },
            // Phone = new TelephoneNumber
            // {
            //     FreeFormNumber = lead.NormalizedPhoneNumber,
            // },
            ApplyTaxAfterDiscount = true,
            ApplyTaxAfterDiscountSpecified = true,
            Line = GetLines(otherItems, taxGroupsMap).ToArray(),
            DocNumber = proposalNumber,
        };

        return invoice;

        //TxnTaxDetail
        // TxnTaxDetail txnTaxDetail = new TxnTaxDetail();
        // txnTaxDetail.TxnTaxCodeRef = new ReferenceType()
        // {
        //     name = stateTaxCode.Name,
        //     Value = stateTaxCode.Id
        // };
        // Line taxLine = new Line();
        // taxLine.DetailType = LineDetailTypeEnum.TaxLineDetail;
        // TaxLineDetail taxLineDetail = new TaxLineDetail();
        // //Assigning the fist Tax Rate in this Tax Code
        // taxLineDetail.TaxRateRef = stateTaxCode.SalesTaxRateList.TaxRateDetail[0].TaxRateRef;
        // taxLine.AnyIntuitObject = taxLineDetail;
        // txnTaxDetail.TaxLine = new Line[] { taxLine };
        // invoice.TxnTaxDetail = txnTaxDetail;
    }

    private void GroupItems()
    {
        var found = new Dictionary<string, AbstractSfLineItem>();

        _items = group().ToArray();
        return;

        IEnumerable<AbstractSfLineItem> group()
        {
            foreach (var item in _items)
            {
                string key = $"{item.Product}{item.Description}";
                if (!found.TryGetValue(key, out var existing))
                {
                    found.Add(key, item);
                    yield return item;
                    continue;
                }

                existing.AdjustedQuantity += item.AdjustedQuantity;
                existing.Quantity += item.Quantity;
                existing.TotalPrice += item.TotalPrice;
                existing.TaxPrice += item.TaxPrice;
                existing.TotalBeforeTax += item.TotalBeforeTax;
                existing.TotalCost += item.TotalCost;

                existing.UnitPrice = (existing.TotalBeforeTax.Value / existing.AdjustedQuantity.Value);
            }
        }
    }

    private IEnumerable<Line> GetLines(Dictionary<string, string> otherItems, Dictionary<string, TaxCode> taxGroups)
    {
        foreach (var item in _items)
        {
            var detail = new SalesItemLineDetail
            {
                Qty = item.AdjustedQuantity ?? item.Quantity ?? 0M,
                QtySpecified = item.AdjustedQuantity.HasValue || item.Quantity.HasValue,
                // DiscountAmt = 
                // UOMRef = new UOMRef
                // {
                //     Unit = 
                // }
                // specify unit price
                // TaxCodeRef = 
            };

            if (item.TaxGroup != null && taxGroups.TryGetValue(item.TaxGroup, out var taxGroup))
            {
                // explicitly assign tax group
                detail.TaxCodeRef = new ReferenceType
                {
                    Value = taxGroup.Id,
                };
            }
            else
            {
                // magic codes, may only work in US
                // will use the "default" for "taxable" and "non taxable"
                if (item.TaxFactor > 0)
                {
                    detail.TaxCodeRef = new ReferenceType
                    {
                        Value = "TAX",
                    };
                }
                else
                {
                    detail.TaxCodeRef = new ReferenceType
                    {
                        Value = "NON",
                    };
                }
            }

            if (UseBeforeTaxPrice)
            {
                // var blendedUnitPrice = (item.TotalPrice.Value / item.AdjustedQuantity.Value);
                detail.AnyIntuitObject = item.UnitPrice; // item.UnitPrice ?? 0,
                detail.ItemElementName = ItemChoiceType.UnitPrice;
            }
            else
            {
                // detail.AnyIntuitObject = (item.TotalPrice.Value / item.AdjustedQuantity.Value); // item.UnitPrice ?? 0,
                // detail.ItemElementName = ItemChoiceType.UnitPrice;
                detail.ItemElementName = ItemChoiceType.UnitCostPrice;
            }

            var linePrice = UseBeforeTaxPrice ? item.TotalBeforeTax : item.TotalPrice;
            var calcPrice = detail.Qty * item.UnitPrice;
            if (linePrice != calcPrice)
            {
                var off = linePrice.HasValue && calcPrice.HasValue ? linePrice - calcPrice : null;
                if (!off.HasValue || Math.Abs(off.Value) > (decimal).01)
                {
                    Errors.Add($"Line Price mismatch for {item.Name}: {detail.Qty} * ${item.UnitPrice} = ${calcPrice} != ${linePrice}");
                }

                linePrice = calcPrice;
            }

            var description = $"{item.Name}: {item.Description}";
            if (_catalogItems.TryGetValue(item.Product, out var product))
            {
                if (_qbItems.TryGetValue(product.Id, out var qbItem))
                {
                    detail.ItemRef = new ReferenceType
                    {
                        Value = qbItem.ExternalId,
                    };

                    description = item.Description ?? item.Name;
                }
            }
            else if (otherItems.TryGetValue(item.Name, out var qbItemId))
            {
                // item exists in Qb but it is not in PI
                detail.ItemRef = new ReferenceType
                {
                    Value = qbItemId,
                };

                description = item.Description ?? item.Name;
            }

            yield return new Line
            {
                Description = description,
                Amount = linePrice ?? 0,
                AmountSpecified = linePrice.HasValue,
                // cost does not work for estimates?
                CostAmount = item.TotalCost ?? 0,
                CostAmountSpecified = item.TotalCost.HasValue,
                // LinkedTxn
                DetailType = LineDetailTypeEnum.SalesItemLineDetail,
                DetailTypeSpecified = true,
                AnyIntuitObject = detail,
            };
        }
    }
}