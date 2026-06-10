using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Newtonsoft.Json;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using MenuItem = PI.Shared.Form.Models.MenuItem;

namespace PI.Shared.Services;

public partial class ObjectTypeService
{
    public class GetUserActionsMenuContext
    {
        public IEntityContext Context { get; init; }
        public ObjectType ObjectType { get; init; }
        public Guid? ObjectId { get; init; }
        public Guid? AppDataViewId { get; init; }
        public bool IncludeMultiple { get; init; }
        public IDictionary<string, object> FlatObject { get; set; }
        public Guid? FlowId { get; set; }
        public Guid? ObjectStatusId { get; set; }
        public Dictionary<string, ObjectWithType> Objects { get; set; }
        public ExpandoObject ObjectContext { get; set; }

        /// <summary>
        /// Whether to skip to NextUrl (true) or build action: uri (false), 
        ///     when there is no form and the next url
        /// Original behavior as to "skip" (true)
        /// </summary>
        public bool SkipToNextUrlWhenNotForm { get; set; } = true;
    }

    /// <summary>
    /// Get menu item actions for object Type (to be included in dataView)
    /// </summary>
    public async Task<(bool AllowNone, MenuItem[] Items)> GetUserActionsMenuItemsAsync(IEntityContext context, ObjectType objectType, Guid? objectId = null, Guid? appDataViewId = null, bool includeMultiple = false, IDictionary<string, object> flatObject = null)
    {
        var c = new GetUserActionsMenuContext
        {
            Context = context,
            ObjectType = objectType,
            ObjectId = objectId,
            AppDataViewId = appDataViewId,
            IncludeMultiple = includeMultiple,
            FlatObject = flatObject,
        };

        return await UserActionsMenuItemsAsync(c);
    }

    public async Task<(bool AllowNone, MenuItem[] Items)> UserActionsMenuItemsAsync(GetUserActionsMenuContext c)
    {
        var (allowNone, items) = await GetMenuItemsForUserActionsAsync(c);
        if (c.FlatObject == null) return (allowNone, items);

        var fieldLinks = GetMenuItemsFromFieldLinkUserActions(c);
        if (fieldLinks?.Length > 0)
        {
            items = items.Concat(fieldLinks).ToArray();
        }

        var relatedObjects = GetMenuForRelatedUserActions(c);
        if (relatedObjects != null)
        {
            items = items.Append(relatedObjects).ToArray();
        }

        return (allowNone, items);
    }

    private MenuItem GetMenuForRelatedUserActions(GetUserActionsMenuContext c)
    {
        var relatedActions = Array.Empty<MenuItem>();
        var relatedObjectTypes = c.ObjectType.RelatedObjectTypes?.Where(x => x.RBAC.CanRead(c.Context)).ToArray();
        if (relatedObjectTypes?.Length > 0)
        {
            relatedActions = relatedObjectTypes
                .Where(x => x.RelationType == RelationType.OneToOne && x.Criteria?.Conditions?.Length == 1)
                .Select(x =>
                {
                    var condition = x.Criteria.Conditions[0];
                    if (condition.FieldName != Model.IdFieldName) return null;
                    if (condition.Value is not string fieldName) return null;
                    if (!c.FlatObject.TryResolvePathValue(fieldName, out var fieldValue)) return null;

                    return new ActionMenuItem
                    {
                        Name = x.Name,
                        Label = x.Label,
                        Action = $"dataForm://api/v1/CustomObject/{x.ObjectType}({fieldValue})/View",
                        Icon = nameof(Icons.View),
                    };
                })
                .Where(x => x != null)
                .ToArray<MenuItem>();

            var oneToMany = relatedObjectTypes
                .Where(x => x.RelationType == RelationType.OneToMany && x.Criteria?.Conditions?.Length > 0)
                .Select(x =>
                {
                    var url = default(string);
                    foreach (var condition in x.Criteria.Conditions)
                    {
                        if (condition.Value is not string fieldName) return null;
                        if (!c.FlatObject.TryResolvePathValue(fieldName, out var fieldValue)) return null;

                        url = url == null ? $"{condition.FieldName}={fieldValue}" : $"&{condition.FieldName}={fieldValue}";
                    }

                    url = $"dataGrid:/api/v1/CustomObject/{x.ObjectType}?" + url;

                    return new ActionMenuItem
                    {
                        Name = x.Name,
                        Label = x.Label,
                        Action = url,
                        Icon = nameof(Icons.Grid),
                    };
                })
                .Where(x => x != null)
                .ToArray<MenuItem>();

            if (oneToMany.Length > 0)
            {
                relatedActions = relatedActions.Concat(oneToMany).ToArray();
            }
        }

        var referenceFieldActions = c.ObjectType.Fields
            .Where(x => x.Value.RBAC.CanRead(c.Context))
            .Select(x => x.Value?.Field)
            .Where(x => string.IsNullOrEmpty(x.Options?.LinkUrl))
            .OfType<ReferenceField>()
            .Where(x => !string.IsNullOrWhiteSpace(x.ReferenceFieldOptions?.ObjectType))
            .Select(x =>
            {
                if (!c.FlatObject.TryResolvePathValue(x.Name, out var fieldValue) || fieldValue == null) return null;
                return new ActionMenuItem
                {
                    Name = x.Name,
                    Label = x.Label,
                    Action = $"dataForm://api/v1/CustomObject/{x.ReferenceFieldOptions.ObjectType}({fieldValue})/View",
                    Icon = nameof(Icons.View),
                };
            })
            .Where(x => x != null)
            .ToArray<MenuItem>();

        if (referenceFieldActions.Length > 0)
        {
            relatedActions = relatedActions.Concat(referenceFieldActions).ToArray();
        }

        return relatedActions.Length > 0
            ? new Menu
            {
                Name = "RelatedObjects",
                Label = "Related",
                Items = relatedActions.OrderBy(x => x.Label ?? x.Name).ToArray(),
                Icon = nameof(Icons.More),
            }
            : null;
    }

    private MenuItem[] GetMenuItemsFromFieldLinkUserActions(GetUserActionsMenuContext c)
    {
        var fieldLinkActions = c.ObjectType.Fields
            .Where(x => x.Value.RBAC.CanRead(c.Context))
            .Select(x => x.Value?.Field)
            .Where(x => !string.IsNullOrEmpty(x.Options?.LinkUrl))
            .Select(x =>
            {
                if (!c.FlatObject.TryResolvePathValue(x.Name, out var fieldValue)) return null;

                // TODO: should use the entire context so it can refer to other fields 
                // ...
                var linkUrl = x.Options.LinkUrl
                    .Replace("{{id}}", c.ObjectId.ToString())
                    .Replace("{{value}}", fieldValue.ToString());

                if (linkUrl.Contains("{{")) return null;

                var label = x.Options.LinkLabel ?? $"{x.Label}";

                // TODO: check if label is not a template
                // ...

                var index = linkUrl.IndexOf(':');
                var protocol = index >= 0 ? linkUrl[..index]?.ToLowerInvariant() : null;
                return new ActionMenuItem
                {
                    Name = x.Name,
                    Label = label,
                    Action = linkUrl,
                    Visible = new[] { "selectedCount=='1'" },
                    Icon = protocol switch
                    {
                        "mailto" => nameof(Icons.Email),
                        "tel" or "callto" => nameof(Icons.Call),
                        "sms" => nameof(Icons.SMS),
                        "http" or "https" => null, // TOOO: add launch icon
                        "page" => nameof(Icons.Expand), // TODO: replace with icon for page
                        "dataform" => nameof(Icons.View), // TODO: add icon for form
                        "dataview" => nameof(Icons.Grid), // TODO: add icon for grid
                        _ => null
                    }
                };
            })
            .Where(x => x != null)
            .ToArray<MenuItem>();

        return fieldLinkActions;
    }

    private async Task<(bool AllowNone, MenuItem[] Items)> GetMenuItemsForUserActionsAsync(GetUserActionsMenuContext c)
    {
        switch (c.Context.Role)
        {
            case EntityRoleId.Admin:
            case EntityRoleId.Manager:
            case EntityRoleId.User:
            case EntityRoleId.Profile:
                break;

            default:
                // invalid context to run actions
                return (false, Array.Empty<MenuItem>());
        }

        if (c.ObjectId.HasValue)
        {
            c.FlatObject ??= await GetFlatObjectAsync(c.Context, c.ObjectType, c.ObjectId.Value);

            c.ObjectStatusId = c.FlatObject.TryGetValue(nameof(IFlowObject.ObjectStatusId), out var objectStatusObj)
                               && objectStatusObj.TryToParseObjectId(out var objectStatusId)
                ? objectStatusId
                : null;

            c.FlowId = c.FlatObject.TryGetValue(nameof(IFlowObject.FlowId), out var flowIdObj)
                       && flowIdObj.TryToParseObjectId(out var flowId)
                ? flowId
                : null;
        }

        string objectTypeName;
        if (c.ObjectType.UsesDefaultFlow)
        {
            objectTypeName = c.ObjectType.FullName;
        }
        else if (!c.ObjectType.TryGetObjectTypeFromFlowField(out objectTypeName)) // find object from flow field
        {
            objectTypeName = null;
        }

        if (objectTypeName == null)
        {
            _logger.LogInformation("Failed to figured out flow field for {ObjectType}", c.ObjectType.FullName);
            return (false, Array.Empty<MenuItem>());
        }

        c.Objects = new Dictionary<string, ObjectWithType>
        {
            {
                FlowRun.GetObjectAlias(objectTypeName), new ObjectWithType
                {
                    ObjectType = objectTypeName,
                    Object = c.FlatObject
                }
            },
        };

        var query = _connection.Filter<EventType>()
            .Eq(x => x.AccountId, c.Context.AccountId.Value)
            .Eq(x => x.ObjectType, objectTypeName)
            .In(x => x.EntityId, c.Context.GetEntityIds())
            .OfTypeBuilder<EventType, Trigger, UserTrigger>(x => x.Trigger,
                q => UserTriggerQuery(c.Context, q)
                    .Ne(x => x.IsHidden, true)
            );

        if (c.FlowId.HasValue)
        {
            var flow = await _connection.Filter<Flow>()
                .Eq(x => x.AccountId, c.Context.AccountId.Value)
                .Eq(x => x.Id, c.FlowId.Value)
                .Eq(x => x.ObjectType, objectTypeName)
                .FirstOrDefaultAsync();

            if (flow?.Steps == null) return (false, Array.Empty<MenuItem>());

            var eventTypeIds = flow.Steps
                    .Where(x => !x.CurrentStatusId.HasValue || !c.ObjectStatusId.HasValue || x.CurrentStatusId.Value == c.ObjectStatusId.Value)
                    .Select(x => x.EventIdTrigger)
                ;

            query.In(x => x.Id, eventTypeIds);
        }

        var list = await query.FindAsync();
        if (list.Any(x => x.Trigger is UserTrigger userTrigger && userTrigger.Conditions?.Length > 0))
        {
            c.ObjectContext ??= BuildHandlebarsContext(c.FlatObject, c.ObjectId, c.Objects);
            list.RemoveAll(filterOut);
        }

        return await ToMenuAsync(c, list);

        bool filterOut(EventType eventType)
        {
            return eventType.Trigger is UserTrigger userTrigger && userTrigger.Conditions.AnyFalseUsingExpressions(c.Context, c.ObjectContext);
        }
    }

    private async Task<(bool AllowNone, MenuItem[] Actions)> ToMenuAsync(GetUserActionsMenuContext c, List<EventType> list)
    {
        var allowNone = false;
        var result = new List<MenuItem>();

        foreach (var item in list)
        {
            if (item.Trigger is not UserTrigger trigger) continue;
            if (c.ObjectId.HasValue && ((trigger.AllowMultiple && !c.IncludeMultiple) || trigger.AllowNone)) continue;

            if (!c.ObjectId.HasValue && !trigger.AllowMultiple && !trigger.AllowNone) continue;

            if (trigger.ObjectStatusId.HasValue)
            {
                if (c.ObjectId.HasValue && (!c.ObjectStatusId.HasValue || c.ObjectStatusId.Value != trigger.ObjectStatusId.Value))
                {
                    // getting actions for a specif object and the status doesn't match
                    continue;
                }

                if (!trigger.AllowMultiple && !c.ObjectId.HasValue)
                {
                    // object specific and we don't know the object (status)
                    continue;
                }
            }

            var label = trigger.Name;
            if (!label.EndsWith("...")) label += "...";

            if (!string.IsNullOrEmpty(trigger.SnapshotObjectType))
            {
                // view based 
                if (!c.ObjectId.HasValue && c.AppDataViewId.HasValue)
                {
                    // yield return new ActionMenuItem
                    // {
                    //     Name = trigger.Name,
                    //     Label = label,
                    //     Action = $"dataForm://api/v1/{objectType.Name}/AppDataView({appDataViewId})/UserAction({item.Id})",
                    //     Enable = new[] { "selectedCount=='31415926'" },
                    // };

                    // allow taking snapshot implicitly
                    result.Add(
                        new ActionMenuItem
                        {
                            Name = trigger.Name,
                            Label = label,
                            Action = $"action://api/v1/{item.ObjectType}/UserAction({item.Id})",
                            Visible = new[] { "selectedCount=='0'" },
                            Icon = trigger.Icon,
                        }
                    );

                    allowNone = true;
                }

                continue;
            }

            // item(s) based                 
            var action = c.ObjectId.HasValue
                ? $"dataForm://api/v1/{c.ObjectType.FullName}({c.ObjectId})/UserAction({item.Id})"
                : (
                    trigger.AllowMultiple || trigger.AllowNone ? $"dataForm://api/v1/{c.ObjectType.FullName}/UserAction({item.Id})" : $"dataForm://api/v1/{c.ObjectType.FullName}(" + "{{id}}" + $")/UserAction({item.Id})"
                );

            if (trigger.Form == null)
            {
                if (!string.IsNullOrEmpty(trigger.NextUrl) && c.SkipToNextUrlWhenNotForm)
                {
                    // no form with next url defined : skip form and go straight to next url
                    if (c.FlatObject != null)
                    {
                        // has object, allow next url to use template
                        var error = default(string);
                        if (trigger.RelatedObjects?.Length > 0)
                        {
                            error = await LoadRelatedObjectAsync(c.Context, c.Objects, trigger.RelatedObjects, c.ObjectType.FullName);
                        }

                        if (error == null)
                        {
                            c.ObjectContext ??= BuildHandlebarsContext(c.FlatObject, c.ObjectId, c.Objects);
                            action = ProcessNextUrl(c.Context, c.ObjectContext, trigger.NextUrl);
                        }
                        else
                        {
                            _logger.LogError("Failed to load RelatedObjects for {EventTypeId}: {Error}", item.Id, error);
                            action = null;
                        }
                    }
                    else
                    {
                        // use url as it is
                        action = trigger.NextUrl;
                    }

                    if (action == null)
                    {
                        // failed: skip user action
                        continue;
                    }
                }
                else
                {
                    action = c.ObjectId.HasValue ? $"action://api/v1/{c.ObjectType.FullName}({c.ObjectId})/UserAction({item.Id})" : $"action://api/v1/{c.ObjectType.FullName}/UserAction({item.Id})"; // (" + "{{id}}" + $")
                }

                label = trigger.Name;
                if (c.ObjectId.HasValue) action = action.Replace("{{id}}", c.ObjectId.Value.ToString());
            }

            if (!c.ObjectId.HasValue && !trigger.AllowMultiple && trigger.ObjectStatusId.HasValue)
            {
                // action based on status (and we don't have the object)
                result.Add(
                    new ActionMenuItem
                    {
                        Name = trigger.Name,
                        Label = label,
                        Action = action,
                        Visible = new[]
                        {
                            "selectedCount=='1'",
                            $"{nameof(IFlowObject.ObjectStatusId)}=='{trigger.ObjectStatusId}'"
                        },
                        Icon = trigger.Icon ?? nameof(Icons.Action),
                    }
                );
                continue;
            }

            result.Add(
                new ActionMenuItem
                {
                    Name = trigger.Name,
                    Label = label,
                    Action = action,
                    Visible = getVisibleCondition(trigger),
                    Icon = trigger.Icon ?? nameof(Icons.Action),
                }
            );

            allowNone |= trigger.AllowNone;
        }

        return (
            allowNone,
            result
                .OrderBy(x => x.Label ?? x.Name)
                .ToArray()
        );

        string[] getVisibleCondition(UserTrigger trigger)
        {
            if (c.ObjectId.HasValue) return null;
            if (trigger.AllowNone)
            {
                return trigger.AllowMultiple ? null : new[] { "selectedCount=='0'" };
            }

            // multiple
            return new[] { "selectedCount!='0'" };
        }
    }

    private static ExpandoObject BuildHandlebarsContext(IDictionary<string, object> flatObject, Guid? objectId, Dictionary<string, ObjectWithType> objects)
    {
        // if (run != null)
        // {
        //     // TODO: create using run
        //     // ...
        // }

        var ctx = new Dictionary<string, object>
        {
            // { nameof(FlowRun.InitialEvent), evt },
        };

        if (flatObject != null)
        {
            ctx.Add(nameof(FlowRun.InitialObject), flatObject);
            ctx.Add(nameof(FlowRun.Objects), objects.ToDictionary(x => x.Key, x => x.Value.Object));
            ctx.Add("Object", flatObject);
        }

        if (objectId.HasValue)
        {
            ctx["id"] = objectId;
            // ctx["flowRunId"] = objectFlowRunId;
        }

        // if (evt != null)
        // {
        //     context.Add("Event", evt);
        // }

        return JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(ctx));
    }
}