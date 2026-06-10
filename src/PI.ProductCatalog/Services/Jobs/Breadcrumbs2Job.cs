using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PI.ProductCatalog.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.ProductCatalog;

public class Breadcrumbs2Job : IRunJob
{
    private readonly ILogger<Breadcrumbs2Job> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly JobStatusService _jobStatusService;

    public string Name => "Breadcrumbs";

    public CatalogFeed CatalogFeed { get; private set; }
    public List<CatalogItem> Items { get; private set; }
    public Dictionary<Guid, Breadcrumb> Manufacturers { get; private set; }
    public IEntityContext Context { get; private set; }

    public Breadcrumbs2Job(
        ILogger<Breadcrumbs2Job> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        JobStatusService jobStatusService
    )
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _jobStatusService = jobStatusService;
    }

    public async Task<JobResult> ExecuteSingleAsync(Guid id, CancellationToken stoppingToken)
    {
        var feed = await _connection.Filter<CatalogFeed>().Eq(x => x.Id, id).FirstOrDefaultAsync();

        await AddBreadcrumbsAsync(feed);

        return new JobResult
        {
            Message = "finished",
            Result = new Dictionary<string, object>()
        };
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        Context = context;

        var catalogFeedId = Environment.GetEnvironmentVariable("PI_CATALOGFEED");

        if (!string.IsNullOrWhiteSpace(catalogFeedId))
        {
            if (Guid.TryParse(catalogFeedId, out var id))
            {
                return await ExecuteSingleAsync(id, stoppingToken);
            }

            return new JobResult
            {
                Message = $"Error, invalid id: {catalogFeedId}",
                Result = new Dictionary<string, object>()
            };
        }

        var list = await _connection.DipperAggregateAsync<CatalogFeed>("CatalogFeed.BreadcrumbsQueue", "productCatalog");

        _logger.LogInformation("Found {count} feeds", list.Count);

        var start = DateTime.UtcNow;
        var processed = 0;
        var partial = false;
        foreach (var feed in list)
        {
            using var feedScope = _logger.AddScope(new
            {
                CatalogFeed = feed.Id,
                Entity = feed.EntityId,
                File = processed
            });

            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Cancellation was requested");
                break;
            }

            await UpdateBreadcrumbsAsync(context, feed);

            processed++;

            if ((DateTime.UtcNow - start).TotalMinutes > 10)
            {
                _logger.LogInformation("Stopping job after 10 minutes to cooldown");
                partial = true;
                break;
            }
        }

        return new JobResult
        {
            Message = $"{processed} of {list.Count} were recalculated",
            Result = new Dictionary<string, object>
            {
                { "Total", list.Count },
                { "Modified", processed },
                { "Processed", processed },
                { "Partial", partial }
            }
        };
    }

    public async Task UpdateBreadcrumbsAsync(IEntityContext context, CatalogFeed feed)
    {
        var start = DateTime.UtcNow;

        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, feed.EntityId)
            .FirstOrDefaultAsync();

        using var scope = _logger.AddScope(new
        {
            feed.Id,
            feed.EntityId,
            CatalogFeed = feed.Name,
        });

        feed = await _connection.Filter<CatalogFeed>()
            .Eq(x => x.Id, feed.Id)
            .OrBuilder(
                q => q.Exists(x => x.Breadcrumbs.StartedOn, false),
                q => q.Lt(x => x.Breadcrumbs.StartedOn, DateTime.UtcNow.AddHours(-4)),
                q => q.Ne(x => x.Breadcrumbs.EndedOn, null))
            .Update
            .Set(x => x.Breadcrumbs.StartedOn, DateTime.UtcNow)
            .Unset(x => x.Breadcrumbs.EndedOn)
            .UpdateAndGetOneAsync();

        if (feed == null)
        {
            _logger.LogInformation("Skipping feed as it is being procesed by other process");
            return;
        }

        await _jobStatusService.FireSyncStartedAsync(context, feed, "Recalculate Breadcrumbs", new
        {
            Entity = entity?.Name,
            feed.Name
        });

        try
        {
            feed = await AddBreadcrumbsAsync(feed);

            var elapsed = DateTime.UtcNow - start;
            await _jobStatusService.FireSyncFinsihedAsync(
                context,
                feed,
                $"Recalculated Breadcrumbs in {elapsed.TotalSeconds}s",
                new Dictionary<string, object>
                {
                    { "Entity", entity?.Name },
                    { "Name", feed.Name },
                    { "Elapsed", elapsed.TotalMilliseconds }
                }
            );
        }
        catch (Exception ex)
        {
            await _jobStatusService.FireSyncFailedAsync(context, feed, "Recalculate Breadcrumbs", new
            {
                Entity = entity?.Name,
                feed.Name,
            });

            _logger.LogError(ex, "Failed to update breadcrumbs");
        }
    }

    private async Task<CatalogFeed> AddBreadcrumbsAsync(CatalogFeed feed)
    {
        using var scope = _logger.AddScope(new
        {
            feed.AccountId,
            feed.EntityId,
            CatalogFeedId = feed.Id,
        });

        CatalogFeed = feed;

        await StartAsync();
        await LoadManufacturersAsync();
        await AddStylesAsync();

        var sfproducts = await AddSfProductsAsync(
            BreadcrumbType.Manufacturer,
            Manufacturers,
            BreadcrumbType.SfProduct
        );

        var materialTypes = await AddMaterialTypesAsync(sfproducts);

        await _connection.BulkWriteAsync(sfproducts.Select(ToModel), 500);
        sfproducts = null;

        await AddCollectionsAsync(materialTypes);

        var materialSubTypes = await AddMaterialSubTypesAsync(materialTypes);

        await _connection.BulkWriteAsync(materialTypes.Select(ToModel), 500);
        materialTypes = null;

        await AddProductTypesAsync(materialSubTypes);

        await _connection.BulkWriteAsync(materialSubTypes.Select(ToModel), 500);
        materialSubTypes = null;

        await WriteManufacturersAsync();
        await WriteItemsAsync();

        _logger.LogInformation("Flag Catalog as modified");
        var now = DateTime.UtcNow;
        await _connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, CatalogFeed.AccountId)
            .Eq(x => x.Id, CatalogFeed.Id)
            .Update
            .Set(x => x.Breadcrumbs.EndedOn, now)
            .Set(x => x.LastModifiedOn, now)
            .UpdateOneAsync();

        await _objectTypeService.FireObjectUpdatedAsync(
            Context,
            CatalogFeed,
            new Dictionary<string, object>
            {
                { $"{nameof(CatalogFeed.Breadcrumbs)}|{nameof(LongTask.EndedOn)}", now }
            }
        );

        return CatalogFeed;
    }

    private async Task<Breadcrumb[]> AddProductTypesAsync(IEnumerable<Breadcrumb> materialSubTypes)
    {
        var parents = materialSubTypes.ToDictionary(x => x.Id);
        var list = await AddBreadcrumbsAsync<CatalogProductType>(
            BreadcrumbType.MaterialSubType,
            parents,
            BreadcrumbType.ProductType,
            item => item.ProductType // ,
            // (bc, item) => bc.Description = item.ProductType
        );

        await _connection.BulkWriteAsync(list.Select(ToModel), 500);

        return list;
    }

    private async Task<Breadcrumb[]> AddCollectionsAsync(IEnumerable<Breadcrumb> materialTypes)
    {
        var parents = materialTypes.ToDictionary(x => x.Id);
        var list = await AddBreadcrumbsAsync<CatalogCollection>(
            BreadcrumbType.MaterialType,
            parents,
            BreadcrumbType.Collection,
            item => item.CollectionName // ,
            // (bc, item) => bc.Description = item.CollectionName
        );

        await _connection.BulkWriteAsync(list.Select(ToModel), 500);

        return list;
    }

    private async Task<Breadcrumb[]> AddMaterialSubTypesAsync(IEnumerable<Breadcrumb> materialTypes)
    {
        var parents = materialTypes.ToDictionary(x => x.Id);
        var list = await AddBreadcrumbsAsync<CatalogMaterialSubType>(
            BreadcrumbType.MaterialType,
            parents,
            BreadcrumbType.MaterialSubType,
            item => item.Material?.SubType.ToString(),
            (bc, item) => bc.Name = item.Material.SubType.GetDescription()
        );

        return list;
    }

    private async Task<Breadcrumb[]> AddMaterialTypesAsync(IEnumerable<Breadcrumb> sfproducts)
    {
        var parents = sfproducts.ToDictionary(x => x.Id);
        var list = await AddBreadcrumbsAsync<CatalogMaterialType>(
            BreadcrumbType.SfProduct,
            parents,
            BreadcrumbType.MaterialType,
            item => item.Material?.Type.ToString() ?? nameof(MaterialType.Unclassified),
            (bc, item) => bc.Name = item.Material?.Type.GetDescription()
        );

        return list;
    }

    private async Task<Dictionary<string, CatalogStyle>> GetProductSelectorStylesAsync()
    {
        if (CatalogFeed.EntityId == CatalogFeed.AccountId) return null;

        var src = default(CatalogFeed);
        if (CatalogFeed is B2BCatalogFeed b2b)
        {
            // b2b
            src = await _connection.Filter<CatalogFeed, B2BCatalogFeed>()
                .Eq(x => x.AccountId, CatalogFeed.AccountId)
                .Eq(x => x.EntityId, CatalogFeed.AccountId)
                .Eq(x => x.SenderId, b2b.SenderId)
                .Eq(x => x.IsActive, true)
                .FirstOrDefaultAsync();
        }
        else if (CatalogFeed is CloneCatalogFeed clone)
        {
            // clone
            src = await _connection.Filter<CatalogFeed>()
                .Eq(x => x.AccountId, CatalogFeed.AccountId)
                .Eq(x => x.EntityId, CatalogFeed.AccountId)
                .Eq(x => x.Id, clone.CatalogFeedId)
                .Eq(x => x.IsActive, true)
                .FirstOrDefaultAsync();
        }

        if (src == null) return null;

        // could be any tag
        // ...
        var styles = await _connection.Filter<Breadcrumb, CatalogStyle>()
            // .Eq(x=>x.AccountId, src.AccountId)
            // .Eq(x=>x.EntityId, src.EntityId)
            .Eq(x => x.CatalogFeedId, src.Id)
            .AnyEq(x => x.Tags, AbstractCatalogEntity.PRODUCT_SELECTOR)
            .FindAsync();

        if (styles.Count < 1) return null;

        return styles.ToDictionary(x => x.ExternalId);
    }

    private async Task AddStylesAsync()
    {
        var systemTags = new[] { AbstractCatalogEntity.PRODUCT_SELECTOR };

        var srcReferences = await GetProductSelectorStylesAsync();

        var styles = await AddBreadcrumbsAsync<CatalogStyle>(
            BreadcrumbType.Manufacturer,
            Manufacturers,
            BreadcrumbType.Style,
            item => item.StyleNumber,
            initializer,
            preparer,
            accumulator
        );

        await _connection.BulkWriteAsync(styles.Select(x => ToModel(x)), 500);

        static void initializer(CatalogStyle bc, CatalogItem item)
        {
            bc.Name = item.StyleName;
            bc.Material = new MaterialClassification
            {
                Type = item.Material?.Type ?? MaterialType.Unclassified
            };
        }

        void preparer(CatalogStyle bc, Breadcrumb parent)
        {
            bc.Count = 0;

            // reset cost ranges
            bc.ConditionalCostRange = null;
            bc.StandardCostRange = null;
            bc.Margin ??= parent.Margin;

            // copy properties from reference style
            if (srcReferences != null && srcReferences.TryGetValue(bc.ExternalId, out var src))
            {
                if (src.Properties != null && src.Properties.Count > 0)
                {
                    bc.Properties ??= new Dictionary<string, object>();
                    foreach (var prop in src.Properties)
                    {
                        bc.Properties[prop.Key] = prop.Value;
                    }
                }

                bc.Tags = (bc.Tags ?? Enumerable.Empty<string>())
                    .Except(systemTags)
                    .Concat(src.Tags)
                    .Distinct()
                    .ToArray();
            }
        }

        void accumulator(CatalogStyle bc, CatalogItem item)
        {
            if (bc.Material.IsRollGoods)
            {
                if (item.StandardCost != null)
                {
                    // standard == roll (cheaper)
                    bc.ConditionalCostRange ??= new PriceRange();
                    bc.ConditionalCostRange.Append(item.StandardCost.UnitCost);
                }

                if (item.CutCost != null)
                {
                    // cut == more expensive 
                    bc.StandardCostRange ??= new PriceRange();
                    bc.StandardCostRange.Append(item.CutCost.UnitCost);
                }

                var price = item.CutPrice ?? item.StandardPrice;
                if (price.HasValue)
                {
                    bc.StandardPriceRange ??= new PriceRange();
                    bc.StandardPriceRange.Append(price.Value);
                }
            }
            else
            {
                if (item.PalletCost != null)
                {
                    // pallet == cheaper
                    bc.ConditionalCostRange ??= new PriceRange();
                    bc.ConditionalCostRange.Append(item.PalletCost.UnitCost);
                }

                if (item.StandardCost != null)
                {
                    // standard == normal (more expensive)
                    bc.StandardCostRange ??= new PriceRange();
                    bc.StandardCostRange.Append(item.StandardCost.UnitCost);
                }

                var price = item.StandardPrice ?? item.CutPrice;
                if (price.HasValue)
                {
                    bc.StandardPriceRange ??= new PriceRange();
                    bc.StandardPriceRange.Append(price.Value);
                }
            }

            // TODO: this code could be moved into the AddBreadcrumbsAsync so it would propagate from all types of breadcrumbs
            // copy tags (and could be copy properties?)
            if (bc.Tags?.Length > 0)
            {
                // TODO: removing "controlled" tags if missing from the source (e.g. "Product Selector")
                // ...
                // copy tags down to items if any added
                var tags = (item.Tags ?? Enumerable.Empty<string>())
                    .Except(systemTags)
                    .Concat(bc.Tags)
                    .Distinct()
                    .ToArray();

                var oldHash = string.Join(":", (item.Tags ?? Enumerable.Empty<string>()).OrderBy(x => x));
                var newHash = string.Join(":", tags.OrderBy(x => x));
                if (newHash != oldHash)
                {
                    item.Tags = tags;
                    item.LastModifiedOn = DateTime.UtcNow;
                    item.LastActor = Context.Actor();
                }
            }
            
            // copy tags from style (for product selector)
            item.Properties = bc.Properties;
        }
    }

    private async Task WriteItemsAsync()
    {
        var result = await _connection.BulkWriteAsync(Items.Select(getUpdateModel), 2000);

        _logger.LogInformation("CatalogItem Update: {matched} {modified} {total}", result.MatchedCount, result.ModifiedCount, result.RequestCount);

        UpdateOneModel<CatalogItem> getUpdateModel(CatalogItem item)
        {
            // TODO: break into multiple updates per item
            // so we can reduce risk of overwriting user changes?
            // (e.g. only set margin if margin is null, ...)
            // ...
            var update = _connection.Filter<CatalogItem>()
                    .Eq(x => x.Id, item.Id)
                    .Update
                    // update parents
                    .Set(x => x.Parents, item.Parents)
                    .Set(x => x.ParentIds, item.ParentIds)
                ;

            var modified = false;
            if (item.Margin != null)
            {
                update.Set(x => x.Margin, item.Margin);
                modified = true;
            }

            // TODO: handle removing all tags
            // ...
            if (item.Tags?.Length > 0)
            {
                update.Set(x => x.Tags, item.Tags);
                modified = true;
            }

            if (item.Properties?.Count > 0)
            {
                update.Set(x => x.Properties, item.Properties);
                modified = true;
            }
            
            // TODO: could call item.Update() 
            // ...
            
            update
                .SetOrUnset(x => x.Description, item.GetDescription())
                .SetOrUnset(x => x.CutCost, item.CutCost)
                .SetOrUnset(x => x.StandardCost, item.StandardCost)
                .SetOrUnset(x => x.PalletCost, item.PalletCost)
                // prices
                .SetOrUnset(x => x.CutPrice, item.CutPrice)
                .SetOrUnset(x => x.StandardPrice, item.StandardPrice)
                .SetOrUnset(x => x.PalletPrice, item.PalletPrice)
                .SetOrUnset(x => x.Prices, item.Prices)
                ;

            // .SetOrUnset(x => x.KeyDates, item.KeyDates)
            // .SetOrUnset(x => x.Costs, item.Costs)
            // .SetOrUnset(x => x.IsActive, item.IsActive)
            
            if (modified)
            {
                // TODO: don't think it has any meaning, the modified data has already been updated when the process started
                // LastModifiedOn/LastActor will have only changed if
                // - a margin was added or;
                // - tags modified
                // - properties copied from style 
                update
                    .Set(x => x.LastModifiedOn, item.LastModifiedOn)
                    .Set(x => x.LastActor, item.LastActor)
                    ;
            }

            return update.UpdateOneModel();
        }
    }

    private async Task WriteManufacturersAsync()
    {
        var result = await _connection.BulkWriteAsync(Manufacturers.Values.Select(ToModel), 500);
        _logger.LogInformation("{modified} {inserted} manufacturer(s)", result.ModifiedCount, result.InsertedCount);
    }

    private WriteModel<Breadcrumb> ToModel(Breadcrumb breadcrumb)
    {
        if (breadcrumb.ParentIds == null || breadcrumb.ParentIds.Length < 1)
        {
            // throw new Exception("Failed to find parents");
            _logger.LogInformation("No parents found for {type} {id}: {isUpdate}", breadcrumb.Type, breadcrumb.Id, breadcrumb.LastModifiedOn.HasValue);
        }

        if (breadcrumb.LastModifiedOn.HasValue)
        {
            // update
            var update = _connection.Filter<Breadcrumb>()
                    .Eq(x => x.Id, breadcrumb.Id)
                    .Update
                    .SetOrUnset(x => x.Parents, breadcrumb.Parents)
                    .SetOrUnset(x => x.ParentIds, breadcrumb.ParentIds)
                    .Set(x => x.Children, breadcrumb.Children)
                    .Set(x => x.Count, breadcrumb.Count)
                    .Set(x => x.LastModifiedOn, breadcrumb.LastModifiedOn)
                    .Set(x => x.LastActor, breadcrumb.LastActor)
                    .Set(x => x.IsActive, breadcrumb.Count > 0)
                ;

            if (breadcrumb is CatalogStyle style)
            {
                // other properties are not change by breadcrumbs
                update
                    .SetOrUnset(nameof(CatalogStyle.ConditionalCostRange), style.ConditionalCostRange)
                    .SetOrUnset(nameof(CatalogStyle.StandardCostRange), style.StandardCostRange)
                    .SetOrUnset(nameof(CatalogStyle.CostRange), style.CostRange)
                    .SetOrUnset(nameof(CatalogStyle.PriceRange), style.PriceRange)
                    .SetOrUnset(nameof(CatalogStyle.Margin), style.Margin)
                    .SetOrUnset(nameof(CatalogStyle.Tags), style.Tags)
                    .SetOrUnset(nameof(CatalogStyle.Properties), style.Properties)
                    ;
            }

            return update.UpdateOneModel();
        }

        // new record
        return new InsertOneModel<Breadcrumb>(breadcrumb);
    }

    private async Task<Breadcrumb[]> AddSfProductsAsync(
        BreadcrumbType parentType,
        Dictionary<Guid, Breadcrumb> parents,
        BreadcrumbType bcType)
    {
        var parentBreadcrumbs = parents.Values.ToDictionary(x => x.Id, x => new Dictionary<string, Breadcrumb>());

        var materialTypeLookup = await _connection.Filter<CustomObject>()
            .Eq(x => x.AccountId, CatalogFeed.AccountId)
            .Eq(x => x.EntityId, CatalogFeed.EntityId)
            .Eq(x => x.ObjectType, "PlMaterialTypeLookup")
            .Eq(x => x.IsActive, true)
            .FindAsync();

        var sfProduct = materialTypeLookup.ToDictionary(x => x.ExternalId);

        var breadcrumbs = await _connection.Filter<Breadcrumb, CatalogSfProduct>()
            .Eq(x => x.AccountId, CatalogFeed.AccountId)
            .Eq(x => x.EntityId, CatalogFeed.EntityId)
            .Eq(x => x.CatalogFeedId, CatalogFeed.Id)
            // .Eq(x => x.Type, bcType)
            .FindAsync();

        foreach (var bc in breadcrumbs)
        {
            if (!parentBreadcrumbs.TryGetValue(bc.ParentId, out var bcs))
            {
                // TODO: probably worth deleting to clean up
                // breadcrumb parent no longer exist, just ignore it for now
                // throw new Exception($"{parentType} not found: {bc.ParentId}");
                continue;
            }

            bc.Count = 0;
            bcs.Add(bc.ExternalId, bc);
        }

        var notFound = new HashSet<string>();
        foreach (var item in Items)
        {
            if (!item.IsActive) continue;

            // lookup based on material type
            var materialKey = item.GetMaterialTypeLookupValue();
            if (!sfProduct.TryGetValue(materialKey, out var product))
            {
                if (!notFound.Contains(materialKey))
                {
                    _logger.LogInformation("{material} is not mapped", materialKey);
                    notFound.Add(materialKey);
                }

                continue;
            }

            if (!product.TryGetProperty<string>("SfProductExternalId", out var breadCrumExternalId))
            {
                throw new Exception("SfProductExternalId property missing");
            }

            // find parent
            var parentId = item.Parents[parentType.ToString()];
            var parent = parents[parentId];
            if (!parentBreadcrumbs.TryGetValue(parentId, out var children))
            {
                throw new Exception($"{parentType} not found: {parentId}");
            }

            if (!children.TryGetValue(breadCrumExternalId, out var bc))
            {
                // first
                bc = CreateBreadcrumb<CatalogSfProduct>(breadCrumExternalId, parent);
                children.Add(bc.ExternalId, bc);
            }
            else
            {
                // increment count
                if (bc.Count == 0)
                {
                    // first, mark as modified so it will updated
                    bc.LastActor = Context.Actor();
                    bc.LastModifiedOn = DateTime.UtcNow;
                }

                bc.Count++;
            }

            bc.Parents ??= new Dictionary<string, Guid>(parent.Parents)
            {
                { parentType.ToString(), parent.Id }
            };

            // change parents (but do not mark the item as modified)
            item.Parents[bcType.ToString()] = bc.Id;

            // other properties
            if (!item.Margin.HasValue && product.TryGetProperty<decimal?>("Margin", out var margin))
            {
                // update margin and last modified so it will be synced to salesforce
                item.Margin = margin;
                item.LastModifiedOn = DateTime.UtcNow;
                item.LastActor = Context.Actor();
            }
        }

        foreach (var kvp in parentBreadcrumbs)
        {
            var parent = parents[kvp.Key];
            var count = kvp.Value.Count(x => x.Value.Count > 0);
            parent.SetChildrenCount(bcType.ToString(), count);
        }

        // write
        var list = parentBreadcrumbs
            .SelectMany(x => x.Value.Where(x => x.Value.Count > 0))
            .Select(x => x.Value)
            .ToArray();

        return list;
    }

    private async Task<Breadcrumb[]> AddBreadcrumbsAsync<T>(
        BreadcrumbType parentType,
        Dictionary<Guid, Breadcrumb> parents,
        BreadcrumbType bcType,
        Func<CatalogItem, string> externalIdResolver,
        Action<T, CatalogItem> initializer = null,
        Action<T, Breadcrumb> prepare = null,
        Action<T, CatalogItem> accumulator = null
    )
        where T : Breadcrumb, new()
    {
        var parentBreadcrumbs = parents.Values.ToDictionary(x => x.Id, x => new Dictionary<string, T>());

        var breadcrumbs = await _connection.Filter<Breadcrumb, T>()
            .Eq(x => x.AccountId, CatalogFeed.AccountId)
            .Eq(x => x.EntityId, CatalogFeed.EntityId)
            .Eq(x => x.CatalogFeedId, CatalogFeed.Id)
            // .Eq(x => x.Type, bcType)
            .FindAsync();

        foreach (var bc in breadcrumbs)
        {
            if (!parentBreadcrumbs.TryGetValue(bc.ParentId, out var bcs))
            {
                // parent didn't have any interesting items?
                // skip
                // _logger.LogInformation("{parentType} not found for {parentId} for {breadcrumbId}", parentType, bc.ParentId, bc.Id);
                continue;
            }

            var parent = parents[bc.ParentId];
            if (prepare != null)
            {
                prepare.Invoke(bc, parent);
            }
            else
            {
                bc.Count = 0;
                bc.Margin ??= parent?.Margin;
            }

            bcs.Add(bc.ExternalId, bc);
        }

        foreach (var item in Items)
        {
            if (!item.IsActive) continue;

            var bcExternalId = externalIdResolver(item);
            if (string.IsNullOrWhiteSpace(bcExternalId))
            {
                _logger.LogDebug("{item} missing {type}", item.Id, bcType);
                continue;
            }

            // find parent
            if (!item.Parents.TryGetValue(parentType.ToString(), out var parentId))
            {
                _logger.LogDebug("{item} not assigned to any {type}", item.Id, parentType);
                continue;
            }

            var parent = parents[parentId];
            if (!parentBreadcrumbs.TryGetValue(parentId, out var children))
            {
                // throw new Exception($"{parentType} not found: {parentId}");
                _logger.LogInformation("{parentType} not found for {parentId} for {itemId}", parentType, parentId, item.Id);
                continue;
            }

            if (!children.TryGetValue(bcExternalId, out var bc))
            {
                // first 
                bc = CreateBreadcrumb<T>(bcExternalId, parent);
                initializer?.Invoke(bc, item);

                children.Add(bc.ExternalId, bc);
            }
            else
            {
                // increment count
                if (bc.Count == 0)
                {
                    // first, mark as modified
                    bc.LastActor = Context.Actor();
                    bc.LastModifiedOn = DateTime.UtcNow;
                }

                bc.Count++;
            }

            bc.Parents ??= new Dictionary<string, Guid>(parent.Parents)
            {
                { parentType.ToString(), parent.Id }
            };

            item.Parents[bcType.ToString()] = bc.Id;

            accumulator?.Invoke(bc, item);
        }

        // update count
        foreach (var kvp in parentBreadcrumbs)
        {
            var parent = parents[kvp.Key];
            var count = kvp.Value.Count(x => x.Value.Count > 0);
            parent.SetChildrenCount(bcType.ToString(), count);
        }

        var list = parentBreadcrumbs
            .SelectMany(x => x.Value.Where(x => x.Value.Count > 0))
            .Select(x => x.Value)
            .ToArray();

        return list;
    }

    private async Task StartAsync()
    {
        // reset existing breadcrumbs
        await _connection.Filter<Breadcrumb>()
            .Eq(x => x.AccountId, CatalogFeed.AccountId)
            .Eq(x => x.EntityId, CatalogFeed.EntityId)
            .Eq(x => x.CatalogFeedId, CatalogFeed.Id)
            .Update
            .Unset(x => x.Count)
            .Unset(x => x.Children)
            .Unset(x => x.ParentIds)
            .Unset(x => x.Parents)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.IsActive, false)
            .UpdateManyAsync();

        await _connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, CatalogFeed.AccountId)
            .Eq(x => x.EntityId, CatalogFeed.EntityId)
            .Eq(x => x.CatalogFeedId, CatalogFeed.Id)
            .Update
            .Unset(x => x.ParentIds)
            .Unset(x => x.Parents)
            .UpdateManyAsync();

        // get all items
        Items = await _connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, CatalogFeed.AccountId)
            .Eq(x => x.EntityId, CatalogFeed.EntityId)
            .Eq(x => x.CatalogFeedId, CatalogFeed.Id)
            .FindAsync();
    }

    private async Task LoadManufacturersAsync()
    {
        var manufacturers = await _connection.Filter<Breadcrumb, CatalogManufacturer>()
            .Eq(x => x.AccountId, CatalogFeed.AccountId)
            .Eq(x => x.EntityId, CatalogFeed.EntityId)
            .Eq(x => x.CatalogFeedId, CatalogFeed.Id)
            // .Eq(x => x.Type, BreadcrumbType.Manufacturer)
            .FindAsync();

        var breadcrumbs = manufacturers.ToDictionary(x => x.ExternalId);

        foreach (var item in Items)
        {
            if (!item.IsActive) continue;

            // always reset
            item.Parents = new Dictionary<string, Guid>
            {
                { nameof(CatalogFeed), CatalogFeed.Id }
            };

            if (string.IsNullOrEmpty(item.Manufacturer)) continue;
            if (!breadcrumbs.TryGetValue(item.Manufacturer, out var bc))
            {
                bc = CreateBreadcrumb<CatalogManufacturer>(item.Manufacturer);
                breadcrumbs.Add(bc.ExternalId, bc);
            }
            else
            {
                if (bc.Count == 0)
                {
                    bc.LastActor = Context.Actor();
                    bc.LastModifiedOn = DateTime.UtcNow;
                    bc.Parents = new Dictionary<string, Guid>
                    {
                        { nameof(CatalogFeed), CatalogFeed.Id }
                    };
                }

                bc.Count++;
            }

            item.Parents.Add(nameof(BreadcrumbType.Manufacturer), bc.Id);
        }

        Manufacturers = breadcrumbs.Values.ToDictionary(x => x.Id, x => (Breadcrumb)x);
    }

    private T CreateBreadcrumb<T>(string name, Breadcrumb parent = null)
        where T : Breadcrumb, new()
    {
        var parents = parent?.Parents != null
            ? new Dictionary<string, Guid>(parent.Parents)
            : new Dictionary<string, Guid>
            {
                { nameof(CatalogFeed), CatalogFeed.Id }
            };

        if (parent != null) parents.Add(parent.Type.ToString(), parent.Id);

        var bc = new T
        {
            Id = Model.NewObjectId(),
            AccountId = CatalogFeed.AccountId,
            EntityId = CatalogFeed.EntityId,
            CatalogFeedId = CatalogFeed.Id,
            Name = name,
            ExternalId = name,
            Count = 1,
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = null, // to indicate it is a new item
            LastActor = Context.Actor(),
            ParentId = parent?.Id ?? CatalogFeed.Id,
            Parents = parents,
            Margin = parent?.Margin,
        };

        return bc;
    }
}