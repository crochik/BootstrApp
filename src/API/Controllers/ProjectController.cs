using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Layout;
using PI.Shared.Services;

namespace Controllers;

[Route("/api/v1/[controller]")]
public class ProjectController : APIController
{
    private readonly ObjectTypeService _objectTypeService;

    public ProjectController(ObjectTypeService objectTypeService)
    {
        _objectTypeService = objectTypeService;
    }

    /// <summary>
    /// hack to build Project page with CC ...
    /// HIDES THE Action to handle getting pages for a project object (CustomObjectController)!!!!!
    /// TODO: replace me 
    /// </summary>
    [Authorize("default")]
    [HttpGet("/api/v1/[controller]({id})/DataPage")]
    // [HttpGet("DataPage")]
    public async Task<Page> GetPageAsync([FromRoute] string id)
    {
        if (!Model.TryParseGuid(id, out var projectId))
        {
            throw new BadRequestException("Invalid project id");
        }

        var objectType = await _objectTypeService.GetAsync(Context, "sf_WorkOrder");
        if (objectType == null || !objectType.CanRead(Context)) throw new ForbiddenException("ObjectType");

        var project = await _objectTypeService.GetExpandoObjectByIdAsync(Context, objectType, projectId);
        if (project == null) throw new ForbiddenException("sf_WorkOrder");

        if (!Context.Claims.TryGetValue("x-companycam-id", out var ccCompanyId))
        {
            // ...
        }

        // TODO: check if user has cc
        // ...

        if (!project.TryResolvePathStrValue("{{CCProjectId}}", out var ccProjectId))
        {
            // ...
        }

        if (!project.TryResolvePathStrValue("{{ExternalId}}", out var externalId)) throw new ForbiddenException("ExternalId");

        var flatObject = await _objectTypeService.RecursivelyFlattenAsync(Context, objectType, project);
        var (_, menuItems) = await _objectTypeService.GetUserActionsMenuItemsAsync(Context, objectType, projectId, null, false, flatObject);

        return new LayoutPage
        {
            AppMenu = string.Empty, // remove menu
            // HideAppBar = 
            Name = "Project",
            Layout = new LayoutContainer
            {
                Type = LayoutContainerType.Tabs,
                Children = tabs().ToArray(),
            },
            Menu = new Menu
            {
                Name = "Actions",
                Icon = nameof(Icons.Action),
                Items = new MenuItem[]
                {
                    new Menu
                    {
                        Name = "Popup",
                        Icon = nameof(Icons.More),
                        Items = menuItems,
                    }
                }
            }
        };

        IEnumerable<LayoutItem> tabs()
        {
            yield return new ObjectLayoutItem
            {
                Name = "Photos",
                Url = new Uri($"datagrid:/salesforce/v1/WorkOrder({externalId})/photo").ToString(),
            };

            if (ccProjectId != null)
            {
                yield return new ObjectLayoutItem
                {
                    Name = "CompanyCam",
                    Url = new Uri($"datagrid:/companycam/v1/project({ccProjectId})/photo").ToString(),
                };
            }
            else
            {
                yield return new ObjectLayoutItem
                {
                    Name = "CompanyCam",
                    Url = new Uri($"https://www.leadspiper.com/integreation/companycam").ToString(),
                };
            }
        }
    }
}