using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
// using KellermanSoftware.CompareNetObjects;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.Shared.Models;

namespace PI.ProductCatalog.Services;

public class Loader
{
    private readonly ILogger<Loader> _logger;
    private readonly IMapper _mapper;
    private readonly IDataService _service;
    private CatalogParserContext _context;
    private Dictionary<string, PropertyInfo> ColorProperties { get; set; }

    public Loader(
        ILogger<Loader> logger,
        IMapper mapper,
        IDataService service
        )
    {
        _logger = logger;
        _mapper = mapper;
        _service = service;

        // var config = new ComparisonConfig
        // {
        //     MaxDifferences = 100,
        // };
        // config.MembersToIgnore.Add($"{nameof(CatalogItem)}.{nameof(CatalogItem.Id)}");
        // config.MembersToIgnore.Add($"{nameof(CatalogItem)}.{nameof(CatalogItem.CreatedOn)}");
        // config.MembersToIgnore.Add($"{nameof(CatalogItem)}.{nameof(CatalogItem.LastModifiedOn)}");
        // config.MembersToIgnore.Add($"{nameof(CatalogItem)}.{nameof(CatalogItem.LastActor)}");
    }

    public void Init(CatalogParserContext context)
    {
        if (_context != null) throw new Exception("Loader instance can't be reused");
        _context = context;

        var excluded = context.Sender.IgnoreColorProperties.ToHashSet();
        if (excluded.Count > 0)
        {
            ColorProperties ??= typeof(CatalogItemUpdate).GetProperties()
                .Where(x => !excluded.Contains(x.Name))
                .ToDictionary(x => x.Name);
        }
        else
        {
            ColorProperties ??= typeof(CatalogItemUpdate).GetProperties().ToDictionary(x => x.Name);
        }
    }

    private T Map<T>(LIN lin) where T : CatalogOperation, new()
    {
        var update = new T
        {
            Id = Model.NewObjectId(),
            AccountId = _context.CatalogUpdate.AccountId,
            EntityId = _context.CatalogUpdate.EntityId,
            CatalogUpdateId = _context.CatalogUpdate.Id,
            LastActor = _context.EntityContext.Actor(),
        };

        switch (update)
        {
            case CatalogItemOperation itemUpdateOperation:
                itemUpdateOperation.Update = _mapper.Map<CatalogItemUpdate>(lin);
                break;

            case CatalogStyleOperation styleUpdateOperation:
                styleUpdateOperation.CatalogFeedId = _context.CatalogUpdate.CatalogFeedId;
                styleUpdateOperation.Style = _mapper.Map<CatalogStyleUpdate>(lin);
                break;
        }

        // copy style prices
        if (lin.CTPs != null)
        {
            foreach (var ctp in lin.CTPs)
            {
                var price = _mapper.Map<ItemCost>(ctp);
                if (price.UnitCost <= 0 || price.UnitCost == 99999.99m)
                {
                    update.OverriddenCosts ??= new List<ItemCost>();
                    update.OverriddenCosts.Add(price);
                    continue;
                }

                update.Costs ??= new List<ItemCost>();
                update.Costs.Add(price);
            }
        }

        return update;
    }

    public async Task QueueAsync(LIN lin)
    {
        if (lin.Items == null)
        {
            // no colors
            await GetAndUpdateItemsAsync(lin);
        }
        else
        {
            foreach (var sln in lin.Items)
            {
                await QueueItemAsync(lin, sln);
            }
        }

        await FlushAsync();
    }

    /// <summary>
    /// Load exising items for style and update them
    /// </summary>
    private async Task GetAndUpdateItemsAsync(LIN lin)
    {
        var op = Map<CatalogStyleOperation>(lin);
        op.Operation = CatalogSyncOperation.New; // ?????

        var existing = await _service.GetItemsAsync(op, lin.StyleNumber);
        
        var list = new List<CatalogItemOperation>();
        foreach (var curr in existing)
        {
            var itemOp = Map<CatalogItemOperation>(lin);
            var item = _mapper.Map<CatalogItem>(itemOp.Update);
            item.CatalogFeedId = _context.CatalogUpdate.CatalogFeedId;
            item.AccountId = _context.CatalogUpdate.AccountId;
            item.EntityId = _context.CatalogUpdate.EntityId;
            item.Costs = op.Costs?.ToArray();
            item.UpdateName(_context.Sender);

            Merge(itemOp, item, curr);
            list.Add(itemOp);
        }

        op.Items = list.ToArray();
        Add(op);
    }

    private async Task<CatalogItemOperation> QueueItemAsync(LIN lin, SLN sln)
    {
        var op = Map<CatalogItemOperation>(lin);
        CopyStyleProperties(sln, op);
        CopyItemPrices(sln, op);

        var item = _mapper.Map<CatalogItem>(op.Update);
        item.CatalogFeedId = _context.CatalogUpdate.CatalogFeedId;
        item.AccountId = _context.CatalogUpdate.AccountId;
        item.EntityId = _context.CatalogUpdate.EntityId;
        item.Costs = op.Costs?.ToArray();
        item.LastModifiedOn = DateTime.UtcNow;
        item.UpdateName(_context.Sender);
        item.Update();

        var existing = await _service.GetItemAsync(_context.CatalogUpdate, sln.SKU);
        if (existing == null)
        {
            // new SKU
            op.Operation = CatalogSyncOperation.New;
            op.MergedItem = item;
            op.Summary = "Item created";

            _service.Add(item);
            Add(op);
            return op;
        }

        Merge(op, item, existing);
        Add(op);

        return op;
    }

    private void Merge(CatalogItemOperation op, CatalogItem item, CatalogItem existing)
    {
        // update id before merge
        item.Id = existing.Id;

        var merger = Merger<CatalogItem, CatalogItem>.Merge(item, existing);

        var except = new HashSet<string>
        {
            nameof(CatalogItem.Id),
            nameof(CatalogItem.CreatedOn),
            // nameof(CatalogItem.BsonExtraElements),
        };

        var updates = merger.Updates.Where(x => !except.Contains(x.Name)).ToArray();

        if (updates.Length < 1)
        {
            op.Operation = CatalogSyncOperation.Unchanged;
            op.MergedItem = null;
            op.Summary = "No changes detected";
            return;
        }

        _service.Update(op, existing, updates);

        op.Operation = CatalogSyncOperation.Update;
        op.MergedItem = merger.Result;
        op.Changes = updates;
        op.Summary = string.Join("\n", updates.Select(x => $"{x.Name}: {FormatToString(x.Previous)} => {FormatToString(x.After)}"));
    }

    private string FormatToString(object value)
    {
        if (value == null) return "∅";
        if (value is string str) return str;

        if (value is IEnumerable<object> en)
        {
            return $"[\n{string.Join(",\n", en.Select(x => FormatToString(x)))}\n]";
        }

        return value.ToString();
    }

    private void CopyItemPrices(SLN sln, CatalogItemOperation task)
    {
        if (sln.CTPs == null) return;

        foreach (var ctp in sln.CTPs)
        {
            if (task.Costs != null)
            {
                // check if overrides style price
                var index = task.Costs.FindIndex(x => x.Criteria == ctp.Criteria &&
                                                      x.UOM == ctp.UOM &&
                                                      x.PackageCondition == ctp.PackageCondition &&
                                                      string.Equals(x.LocationId, ctp.LocationId));

                if (index >= 0)
                {
                    var previous = task.Costs[index];

                    task.OverriddenCosts ??= new List<ItemCost>();
                    task.OverriddenCosts.Add(previous);
                    task.Costs.RemoveAt(index);
                }
            }

            var price = _mapper.Map<ItemCost>(ctp);
            if (price.UnitCost <= 0 || price.UnitCost == 99999.99m)
            {
                task.OverriddenCosts ??= new List<ItemCost>();
                task.OverriddenCosts.Add(price);
                continue;
            }

            task.Costs ??= new List<ItemCost>();
            task.Costs.Add(price);
        }
    }

    private void CopyStyleProperties(SLN src, CatalogItemOperation task)
    {
        var dst = task.Update;
        foreach (var prop in src.GetType().GetProperties())
        {
            if (!ColorProperties.TryGetValue(prop.Name, out var dstProp)) continue;

            // do not override Name property as it may have been calculated based on style+name
            if (prop.Name == nameof(CatalogItemUpdate.Name)) continue;

            var srcValue = prop.GetValue(src);
            if (srcValue == null) continue;

            var currValue = dstProp.GetValue(dst);
            if (Equals(srcValue, currValue)) continue;

            if (currValue != null)
            {
                _logger?.LogInformation(
                    "{property}: override style value of {currValue} with {value}",
                    prop.Name,
                    currValue,
                    srcValue
                );

                task.OverriddenProps ??= new Dictionary<string, object>();
                task.OverriddenProps.Add(prop.Name, currValue);
            }

            dstProp.SetValue(dst, srcValue);
        }
    }

    public Task LogWarningAsync(string message)
    {
        return AppendToLog(
            _context._syncJob,
            $"#{_context.LineNumber}: {_context.Line}",
            $"= {message}"
        );
    }

    public Task LogErrorAsync(Exception ex)
    {
        return AppendToLog(
            _context._syncJob,
            $"#{_context.LineNumber}: {_context.Line}",
            $"= {ex.Message}",
            "=== Aborting..."
        );
    }

    public Task LogErrorAsync(DataElementParserException ex, bool isCritical)
    {
        if (isCritical)
        {
            return AppendToLog(
                _context._syncJob,
                $"#{_context.LineNumber}: {_context.Line}",
                $"> Critical Error on {ex.DataElement} (#{ex.Index})",
                $"= {ex.Message}",
                "=== Aborting..."
            );
        }

        return AppendToLog(
            _context._syncJob,
            $"#{_context.LineNumber}: {_context.Line}",
            $"> Violation on {ex.DataElement} (#{ex.Index})",
            $"= {ex.Message}"
        );
    }

    private async Task AppendToLog(CatalogSyncJob job, params string[] message)
    {
        if (_logger != null)
        {
            foreach (var msg in message) _logger.LogInformation(msg);
        }

        await _service.AppendToLogAsync(job.Id, message);
    }

    private void Add(CatalogOperation op)
    {
        _context._syncJob.ItemsCount++;
        _service.Add(op);
    }
    
    private async Task FlushAsync(bool force = false)
    {
        await _service.FlushAsync(force);
    }

    public async Task FinishAsync()
    {
        await FlushAsync(true);
        // await _adapter.UpdateMarginAsync(_context._syncJob.Id);
    }
}