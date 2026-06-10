using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Salesforce.Models;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Salesforce.Models;
using PI.Shared.Services;

namespace Controllers;

[Route("/salesforce/v1/[controller]")]
public class PhotoController : APIController
{
    private readonly ObjectTypeService _objectTypeService;
    private readonly MongoConnection _connection;

    public PhotoController(ObjectTypeService objectTypeService, MongoConnection connection)
    {
        _objectTypeService = objectTypeService;
        _connection = connection;
    }

    [Authorize("default")]
    [HttpPost("/salesforce/v1/WorkOrder({externalId})/[controller]/DataView")]
    public async Task<IDataViewResponse> DataViewAsync([FromRoute] string externalId, DataViewRequest request)
    {
        var objectType = await _objectTypeService.GetAsync(Context, "sf_WorkOrder");
        if (objectType == null || !objectType.CanRead(Context)) throw new ForbiddenException("ObjectType");

        var project = await _objectTypeService.GetExpandoObjectByExternalIdAsync(Context, objectType, externalId);
        if (project == null) throw new ForbiddenException("sf_WorkOrder");

        var externalLinks = Enumerable.Empty<Dictionary<string, object>>();
        if (project.TryResolvePathStrValue("{{Properties.INET_FloorPlan__c}}", out var floorPlanId))
        {
            // get rooms
            var rooms = await _connection.Filter<SalesforceObject<Room>>("salesforce.INET_Room__c")
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Properties.FloorPlanId, floorPlanId)
                .Ne(x => x.Properties.IsDeleted, true)
                .FindAsync();

            if (rooms.Count > 0)
            {
                // get photos for rooms
                var roomFiles = await _connection.Filter<SalesforceObject<SfExternalLink>>("salesforce.INET_ExternalLink__c")
                    .Eq(x => x.AccountId, Context.AccountId.Value)
                    .Ne(x => x.Properties.IsDeleted, true)
                    .In(x => x.Properties.ParentRoomId, rooms.Select(x => x.ExternalId))
                    .Eq(x=>x.Properties.Type,  "Photo")
                    .FindAsync();

                var roomNames = rooms.ToDictionary(x => x.ExternalId, x => x.Name);
                externalLinks = externalLinks.Concat(roomFiles.Select(x => new Dictionary<string, object>
                {
                    { "ExternalId", x.Properties.ExternalId },
                    { "Name", $"{roomNames[x.Properties.ParentRoomId]}: {x.Properties.Name}" },
                    { "Url", x.Properties.Url },
                }));
            }
        }
        
        var response = new DataViewResponse
        {
            Request = request,
            Options = new ImageGalleryViewOptions
            {
                // ThumbnailUrl = "Thumbnail",
                ImageUrl = "Url",
                Label = "Name",
                HideToolbar = true,
                Width = 300,
                Height = 300,
            },
            View = new DataView
            {
                Name = "Photos",
                Title = "Project Photos",
                Fields = new FormField[]
                {
                    new TextField
                    {
                        Name = "ExternalId"
                    },
                    new ImageField
                    {
                        Name = "Url"
                    }
                },
                FilterForm = null,
                Menu = null,
                KeyField = "ExternalId"
            },
            Result = externalLinks.ToArray(),
        };

        return response;
    }
}