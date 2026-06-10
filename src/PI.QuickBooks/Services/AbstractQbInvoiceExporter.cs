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
using PI.Shared.Salesforce.Models;
using EmailAddress = Intuit.Ipp.Data.EmailAddress;

namespace PI.QuickBooks.Services;

public abstract class AbstractQbInvoiceExporter
{
    protected const bool UseBeforeTaxPrice = true;

    protected readonly ILogger<AbstractQbInvoiceExporter> _logger;
    protected readonly MongoConnection _connection;
    protected readonly QuickBooksService _service;

    public abstract Lead Lead { get; }
    public abstract SfWorkOrder WorkOrder { get; }
    public abstract string ProposalNumber { get; }
    public abstract string ProposalName { get; }
    public abstract string ProposalNotes { get; }

    protected IEntityContext _context;
    protected QbEntity _project;
    protected QbEntity _customer;
    protected Dictionary<string, CatalogItem> _catalogItems;
    protected Dictionary<Guid, QbEntity> _qbItems;
    protected GenericLineItem[] _items;

    protected LocalCache _localCache;

    protected Invoice Invoice { get; set; }

    public List<string> Errors { get; } = new();
    public Preferences Preferences { get; protected set; }

    protected AbstractQbInvoiceExporter(
        ILogger<AbstractQbInvoiceExporter> logger,
        MongoConnection connection,
        QuickBooksService service)
    {
        _logger = logger;
        _connection = connection;
        _service = service;
    }
    
    protected async Task<Result<Invoice>> ExportAllAsync()
    {
        // customer
        if (_customer == null)
        {
            var customerResult = await _service.GetOrCreateAsync(_context, Lead);
            if (!customerResult.IsSuccess)
            {
                Errors.Add($"Failed to Create Customer: {customerResult.Status}");
                return customerResult.ConvertTo<Invoice>();
            }

            _customer = customerResult.Value;
        }

        var projectResult = await _service.GetOrCreateAsync(_context, _customer, WorkOrder, Lead);
        if (!projectResult.IsSuccess)
        {
            Errors.Add($"Failed to Create Project: {projectResult.Status}");
            return projectResult.ConvertTo<Invoice>();
        }

        _project = projectResult.Value;

        Preferences = (await _service.GetAllAsync<Preferences>(_context)).FirstOrDefault();

        Invoice = await BuildInvoiceAsync();

        var result = await _service.ExportAsync(_context, Invoice);
        if (!result.IsSuccess)
        {
            Errors.Add($"Failed to Export Invoice: {result.Status}");
            if (result.Status.Contains("Duplicate Document Number Error"))
            {
                return Result<Invoice>.Unknown($"There is already an invoice in QuickBooks for {Invoice.DocNumber}");
            }

            return result.ConvertTo<Invoice>();
        }

        return result;
    }
    
    protected virtual async Task<Invoice> BuildInvoiceAsync()
    {
        _qbItems = (await _connection.Filter<QbEntity>()
            .Eq(x => x.AccountId, _context.AccountId)
            .Eq(x => x.EntityId, _context.EntityId)
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
            if (_catalogItems.ContainsKey(item.ItemKey)) continue;

            // TODO: try to find in QuickBooks
            var existing = await _service.FindAsync<Item>(_context, nameof(Item.Name), item.Name);
            if (existing.Count == 1)
            {
                var qbItem = existing.First();
                _logger.LogInformation("Found {DisplayName} with {Id}", qbItem.FullyQualifiedName, qbItem.Id);
                otherItems[item.Name] = qbItem.Id;
                Errors.Add($"Item {item.ItemKey} not in Price book but exists in QB: {item.Name} #{qbItem.Id}");
                continue;
            }

            Errors.Add($"Didn't find {item.Name}: {item.ItemKey} in Price book (added as service)");
        }

#if DEBUG
        var proposalNumber = $"{ProposalNumber}.{DateTime.UtcNow:MMddHHmm}";
#else
        var proposalNumber = ProposalNumber ?? $"{DateTime.UtcNow:MMddHHmm}";
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
                Value = $"{ProposalNumber}: {ProposalName}\n{ProposalNotes}",
            },
            BillAddr = new PhysicalAddress
            {
                Line1 = WorkOrder.Street,
                City = WorkOrder.City,
                CountrySubDivisionCode = WorkOrder.State,
                PostalCode = WorkOrder.PostalCode,
                Country = WorkOrder.Country,
            },
            ShipAddr = new PhysicalAddress
            {
                Line1 = WorkOrder.Street,
                City = WorkOrder.City,
                CountrySubDivisionCode = WorkOrder.State,
                PostalCode = WorkOrder.PostalCode,
                Country = WorkOrder.Country,
            },
            BillEmail = new EmailAddress
            {
                Address = Lead.NormalizedEmail,
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

    protected IEnumerable<Line> GetLines(Dictionary<string, string> otherItems, Dictionary<string, TaxCode> taxGroups)
    {
        foreach (var item in _items)
        {
            var detail = new SalesItemLineDetail
            {
                Qty = item.AdjustedQuantity ?? 0M,
                QtySpecified = item.AdjustedQuantity.HasValue,
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
                if (item.IsTaxable)
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
            if (_catalogItems.TryGetValue(item.ItemKey, out var catalogItem))
            {
                if (_qbItems.TryGetValue(catalogItem.Id, out var qbItem))
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

    protected void GroupItems()
    {
        var found = new Dictionary<string, GenericLineItem>();

        _items = group().ToArray();
        return;

        IEnumerable<GenericLineItem> group()
        {
            foreach (var item in _items)
            {
                string key = $"{item.ItemKey}{item.Description}";
                if (!found.TryGetValue(key, out var existing))
                {
                    found.Add(key, item);
                    yield return item;
                    continue;
                }

                existing.AdjustedQuantity += item.AdjustedQuantity;
                // existing.Quantity += item.Quantity;
                existing.TotalPrice += item.TotalPrice;
                // existing.TaxPrice += item.TaxPrice;
                existing.TotalBeforeTax += item.TotalBeforeTax;
                existing.TotalCost += item.TotalCost;

                existing.UnitPrice = (existing.TotalBeforeTax.Value / existing.AdjustedQuantity.Value);
            }
        }
    }
    
    public class GenericLineItem
    {
        public string ItemKey { get; set;  }
        public string Description { get; set; }
        public string Name { get; set; }
        public decimal? TotalCost { get; set;  }
        public decimal? TotalBeforeTax { get; set; }
        public decimal? AdjustedQuantity { get; set;  }
        public decimal? UnitPrice { get; set;  }
        // public decimal? Quantity { get; set; }
        public decimal? TotalPrice { get; set; }
        // public decimal? TaxPrice { get; set; }
        public string TaxGroup { get; set; }
        public bool IsTaxable { get; set; }
    }
}