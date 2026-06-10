using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.ProductCatalog.Edi832;
using PI.ProductCatalog.Models;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Route("/productcatalog/v1/CatalogFeed/b2b")]
public class B2BCatalogFeedController : AbstractCatalogFeedController<B2BCatalogFeed>
{
    private readonly IEnumerable<ICatalogFormat> _senders;

    public B2BCatalogFeedController(ObjectTypeService objectTypeService, IEnumerable<ICatalogFormat> senders) :
        base(objectTypeService)
    {
        _senders = senders;
    }

    [Authorize("default")]
    [HttpPost("DataForm")]
    public async Task<DataFormActionResponse> EditFormOnActionAsync([FromBody] DataFormActionRequest request)
    {
        if (string.Equals(request.Action, FormAction.Add))
        {
            if (request.TryGetParam("#senderId", out var knownSender))
            {
                var selected = _senders.FirstOrDefault(x => string.Equals(x.SenderId, knownSender));
                if (selected == null)
                {
                    // TODO: check database 
                    // ...
                    return new DataFormActionResponse(request, $"Invalid Known Sender: {knownSender}");
                }

                request.Parameters[nameof(B2BCatalogFeed.SenderId)] = selected.SenderId;
                request.Parameters[nameof(B2BCatalogFeed.Name)] = selected.Name;
                request.Parameters[nameof(B2BCatalogFeed.Url)] = selected.Url?.ToString();
            }
        }

        return await OnActionAsync(request);
    }

    [Authorize("managerplus")]
    [HttpPost("CatalogSender/Lookup")]
    public async Task<IEnumerable<ReferenceValue>> CatalogSenderLookupAsync(DataViewRequest request)
    {
        // var criteria = request.Criteria?.ToDictionary(x => x.FieldName) ?? new Dictionary<string, PI.Shared.Models.Expressions.Condition>();
        // var autocomplete = criteria.TryGetValue("#automcomplete", out var ac) && !string.IsNullOrWhiteSpace(ac.Value?.ToString()) ? ac.Value.ToString() : null;

        await Task.CompletedTask;

        // TODO: add from database (the generic ones)
        // ...

        return _senders.Select(x => new ReferenceValue
        {
            Id = x.SenderId,
            Value = x.Name
        }).OrderBy(x => x.Value);
    }
}