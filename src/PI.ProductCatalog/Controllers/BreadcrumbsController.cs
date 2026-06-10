using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Controllers.Models;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace Controllers;

[Route("/productcatalog/v1/[controller]")]
public class BreadcrumbsController : APIController
{
    private readonly ILogger<BreadcrumbsController> _logger;
    private readonly MongoConnection _connection;

    public BreadcrumbsController(
        ILogger<BreadcrumbsController> logger,
        MongoConnection connection
    )
    {
        _logger = logger;
        _connection = connection;
    }

    [Authorize("managerplus")]
    [HttpPost("DataView")]
    [ProducesResponseType(typeof(DataViewResponse), 200)]
    [Produces("text/csv", "application/json")]
    public async Task<DataViewResponse> DataViewAsync([FromBody] DataViewRequest request)
    {
        var entityId = Context.Role switch
        {
            EntityRoleId.Admin => Context.AccountId.Value,
            EntityRoleId.Manager => Context.OrganizationId.Value,
            EntityRoleId.User => Context.OrganizationId.Value,
            _ => throw new ForbiddenException(Context, "Invalid Context")
        };

        return await DataViewAsync(request, entityId);
    }

    [Authorize("admin")]
    [HttpPost("/productcatalog/v1/Entity({entityId})/[controller]/DataView")]
    [ProducesResponseType(typeof(DataViewResponse), 200)]
    [Produces("text/csv", "application/json")]
    public async Task<DataViewResponse> AdminDataViewAsync([FromRoute] Guid entityId, [FromBody] DataViewRequest request)
        => await DataViewAsync(request, entityId);

    [Authorize("managerplus")]
    [HttpPost("Search")]
    public async Task<IEnumerable<BreadcrumbTree>> ChildrenSearchDataViewAsync([FromQuery] string fragment)
    {
        var entityId = Context.Role switch
        {
            EntityRoleId.Admin => Context.AccountId.Value,
            EntityRoleId.Manager => Context.OrganizationId.Value,
            EntityRoleId.User => Context.OrganizationId.Value,
            _ => throw new ForbiddenException(Context, "Invalid Context")
        };

        return await GetChildrenSearchDataViewAsync(fragment, entityId);
    }

    [Authorize("admin")]
    [HttpPost("/productcatalog/v1/Entity({entityId})/[controller]/Search")]
    public async Task<IEnumerable<BreadcrumbTree>> AdminChildrenSearchDataViewAsync([FromRoute] Guid entityId, [FromQuery] string fragment)
        => await GetChildrenSearchDataViewAsync(fragment, entityId);

    private async Task<DataViewResponse> DataViewAsync(DataViewRequest request, Guid entityId)
    {
        var response = new DataViewResponse
        {
            Request = request,
            View = GetDataView(request),
        };

        var result = await GetResultAsync(response, entityId);

        // if (request.Criteria?.Any(x => string.Equals(x.FieldName, "Type")) == true)
        // {
        // response.Result = await AutoExpandAsync(result);
        // }
        // else
        // {
        //     response.Result = result;
        // }

        response.Result = result;

        return response.UpdateFields();
    }

    private async Task<IEnumerable<BreadcrumbTree>> GetChildrenSearchDataViewAsync(string fragment, Guid entityId)
    {
        // Breadcrumb
        var list = await _connection.Filter<BreadcrumbTree>("fcb2b.Breadcrumb")
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, entityId)
            .Ne(x => x.IsActive, false)
            .Gt(x => x.Count, 0)
            // .OrBuilder(
            //     q => q.Regex(x => x.ExternalId, new BsonRegularExpression($"{Regex.Escape(fragment)}", "i")),
            //     q => q.Regex(x => x.Name, new BsonRegularExpression($"{Regex.Escape(fragment)}", "i"))
            // )
            .Text(fragment)
            .FindAsync();

        if (list.IsEmpty())
        {
            return Enumerable.Empty<BreadcrumbTree>();
        }

        // build tree of parents: top => feed => ...
        var topLevel = new BreadcrumbTree
        {
            Name = "[ROOT]",
            Type = "Root"
        };

        var feedIds = list
            .Select(x => x.CatalogFeedId)
            .Distinct()
            .ToArray();

        var feeds = feedIds.IsEmpty()
            ? Enumerable.Empty<CatalogFeed>()
            : await _connection.Filter<CatalogFeed>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.EntityId, entityId)
                .In(x => x.Id, feedIds)
                .FindAsync();

        foreach (var feed in feeds)
        {
            topLevel.ChildrenDict.Add(feed.Id, new BreadcrumbTree
            {
                Id = feed.Id,
                Name = feed.Name,
                Type = nameof(CatalogFeed)
            });
        }

        var parentIds = list
            .Where(x => x.ParentIds != null)
            .SelectMany(x => x.ParentIds)
            .Distinct()
            .ToArray();

        var parents = parentIds.IsEmpty()
            ? new List<BreadcrumbTree>()
            : await _connection.Filter<BreadcrumbTree>("fcb2b.Breadcrumb")
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.EntityId, entityId)
                .In(x => x.Id, parentIds)
                .FindAsync();

        var allDict = new Dictionary<object, BreadcrumbTree>(topLevel.ChildrenDict);
        var found = false;
        do
        {
            found = false;
            for (var c = parents.Count - 1; c >= 0; c--)
            {
                var level = parents[c];
                if (allDict.TryGetValue(level.ParentId, out var parent))
                {
                    allDict.Add(level.Id, level);
                    parent.ChildrenDict.Add(level.Id, level);
                    parents.RemoveAt(c);
                    found = true;
                }
            }
        } while (found);

        // add children to tree
        foreach (var item in list)
        {
            var parentId = item.ParentId;
            if (!allDict.TryGetValue(parentId, out var parent))
            {
                _logger.LogError("Couldn't find {parent} in dict", parentId);
                continue;
            }

            if (!parent.ChildrenDict.TryGetValue(item.Type, out var parentType))
            {
                parentType = new BreadcrumbTree
                {
                    Name = item.Type,
                    Type = "Type",
                };

                parent.ChildrenDict.Add(item.Type, parentType);
            }

            if (parentType.ChildrenDict.TryGetValue(item.Id, out var existing))
            {
                // it is a parent and also a match: skip
            }
            else
            {
                parentType.ChildrenDict.Add(item.Id, item);
            }
        }

        Dump(topLevel, "-");

        return topLevel.ChildrenNodes ?? Enumerable.Empty<BreadcrumbTree>();
    }

    private void Dump(BreadcrumbTree item, string spacer)
    {
        Console.WriteLine($"{spacer}{item.Name} ({item.Type})");
        spacer += "  ";
        if (item.ChildrenNodes != null)
            foreach (var child in item.ChildrenNodes)
            {
                Dump(child, spacer);
            }
    }

    private DataView GetDataView(DataViewRequest request)
    {
        return null;
    }

    private async Task<IEnumerable<Breadcrumb>> AutoExpandAsync(Breadcrumb[] result)
    {
        if (result.Length != 1) return result;

        var children = await _connection.Filter<Breadcrumb>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.ParentId, result[0].Id)
            .SortAsc("_t").SortAsc(x => x.Name)
            .FindAsync();

        return await AutoExpandAsync(children.ToArray());
    }

    private async Task<Breadcrumb[]> GetResultAsync(DataViewResponse resp, Guid entityId)
    {
        var query = _connection.Filter<Breadcrumb>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, entityId)
            .Ne(x => x.IsActive, false)
            .Gt(x => x.Count, 0);

        if (resp.Request.Criteria.TryGetEqCondition("Type", out var typeOf))
        {
            query.Eq("_t", typeOf.Value.ToString());
        }

        if (resp.Request.Criteria.TryGetUidValueFromEqCondition(nameof(Breadcrumb.ParentId), out var parentId))
        {
            query.Eq(x => x.ParentId, parentId);
        }

        query.SortAsc("_t").SortAsc(x => x.Name);

        var result = await query.FindAsync();

        return result.ToArray();
    }
}