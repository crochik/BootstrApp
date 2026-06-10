using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.ProductCatalog.Models;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Layout;
using PI.Shared.Requests;

namespace PI.Shared.Services;

public class ObjectDataViewBuilder
{
    private readonly ILogger<ObjectDataViewBuilder> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly ObjectTypeIntrospector _introspector;

    private IEntityContext Context => _introspector.Context;
    private ObjectType ObjectType => _introspector.ObjectType;
    private DataViewRequest Request { get; set; }
    private Projection Projection { get; set; }

    /// <summary>
    /// Whether to use Api Names when set
    /// </summary>
    public bool UseApiNames { get; set; } = false;

    /// <summary>
    /// Whether to generate {ReferenceField}|Name fields automatically
    /// </summary>
    public bool AutoGenerateReferenceFieldNames { get; set; } = true;

    /// <summary>
    /// Whether to skip the customizations (custom views, default views, user settings,... )
    /// </summary>
    public bool SkipCustomizations { get; set; }

    /// <summary>
    /// Whether to include readable fields that were not requested in the dataview
    /// </summary>
    public bool IncludeHiddenFields { get; set; } = true;

    /// <summary>
    /// Whether to include all fields regardless of the requested fields
    /// </summary>
    public bool IncludeAllFields { get; set; } = false;

    /// <summary>
    /// especial flag to limit search to recent/favorites objects
    /// </summary>
    public bool LimitToRecents { get; set; } = false;

    public ObjectDataViewBuilder(ILogger<ObjectDataViewBuilder> logger, MongoConnection connection, ObjectTypeService objectTypeService, ObjectTypeIntrospector introspector)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _introspector = introspector;
    }

    public async Task InitAsync(IEntityContext context, ObjectType objectType, DataViewRequest request, Projection projection = Projection.Fields)
    {
        _introspector.Context = context;
        await _introspector.IntrospectAsync(objectType);

        Request = request;
        Projection = projection;
    }

    public async Task<DataFormActionResponse> SaveDataViewAsync(IEntityContext context, ObjectType objectType, SaveDataViewRequest request)
    {
        await InitAsync(context, objectType, request.Request);
        return await SaveDataViewAsync(request);
    }

    public async Task<AppDataView> CreateAppDataViewAsync(IEntityContext context, ObjectType objectType, DataViewRequest request)
    {
        await InitAsync(context, objectType, request);
        return await CreateDataViewAsync();
    }

    /// <summary>
    /// Matching values for a field 
    /// </summary>
    public async Task<IEnumerable<ReferenceValue>> LookupAsync(IEntityContext context, ObjectType objectType, DataViewRequest request)
    {
        await InitAsync(context, objectType, request, Projection.Lookup);
        return await LookupAsync();
    }

    /// <summary>
    /// Get top (more frequent) values for a field in the subset
    /// </summary>
    public async Task<IEnumerable<ReferenceValue>> TopValuesAsync(IEntityContext context, ObjectType objectType, DataViewRequest request)
    {
        await InitAsync(context, objectType, request, Projection.TopValues);
        return await LookupAsync(true);
    }

    public async Task<DataViewResponse> BuildDataViewAsync(IEntityContext context, ObjectType objectType, DataViewRequest request, Projection projection = Projection.Fields)
    {
        await InitAsync(context, objectType, request, projection);
        return await BuildDataViewAsync();
    }

    /// <summary>
    /// Create only resultSet
    /// - optionally load dataview to add filters and default fields to be returned
    /// - will not try to calculate Calculated Fields 
    /// </summary>
    public async Task<List<ExpandoObject>> BuildResultSetAsync(IEntityContext context, ObjectType objectType, DataViewRequest request)
    {
        await InitAsync(context, objectType, request);

        var appDataView = default(AppDataView);
        if (!string.IsNullOrEmpty(Request.View))
        {
            // load view
            appDataView = await LoadDataViewAsync();
            if (appDataView == null) return [];

            // TODO: don't know why here we need to copy the fields but in the other places we don't 
            // if it is not need, why it fails here
            // if it is needed for all, should be somewhere else
            // ... 
            request.Fields ??= appDataView.Fields;
        }

        appDataView ??= BuildDataView();

        // ???
        appDataView.StoredProcedure ??= new AggregateStoredProcedure
        {
            Collection = ObjectType.CollectionName,
            DatabaseName = ObjectType.DatabaseName,
        };

        var builder = ObjectTypeDataViewResponseBuilder.New(_connection, _introspector, Request, appDataView, Projection);
        builder.UseApiNames = UseApiNames;
        builder.IncludeHiddenFields = IncludeHiddenFields;
        builder.IncludeAllFields = IncludeAllFields;
        builder.AutoGenerateReferenceFieldNames = AutoGenerateReferenceFieldNames;

        return await builder.BuildResultSetAsync();
    }

    public async Task<DataViewResponse> BuildDataViewAsync(IEntityContext context, ObjectType objectType, AppDataView appDataView, DataViewRequest request, Projection projection = Projection.Fields)
    {
        await InitAsync(context, objectType, request, projection);
        return await BuildDataViewAsync(appDataView);
    }

    [Obsolete]
    public async Task<DataViewResponse> GetChildrenDataViewAsync(IEntityContext context, ObjectType objectType, Guid id, DataViewRequest request, string expandPath)
    {
        await InitAsync(context, objectType, request);
        return await GetChildrenDataViewAsync(id, expandPath);
    }

    private async Task<DataViewResponse> BuildDataViewResponseAsync(AppDataView appDataView)
    {
        var builder = ObjectTypeDataViewResponseBuilder.New(_connection, _introspector, Request, appDataView, Projection);
        builder.UseApiNames = UseApiNames;
        builder.IncludeHiddenFields = IncludeHiddenFields;
        builder.AutoGenerateReferenceFieldNames = AutoGenerateReferenceFieldNames;
        builder.LimitToRecents = LimitToRecents;

        var response = await builder.BuildAsync();

        return response;
    }

    /// <summary>
    /// Build dataview for children objects of this object 
    /// </summary>
    private async Task<DataViewResponse> BuildDataViewResponseAsync(Guid id, AppDataView appDataView, ObjectType childObjectType, ChildrenObjectTypeDataViewResponseBuilder.Breadcrumb[] breadcrumbs)
    {
        var response = await ChildrenObjectTypeDataViewResponseBuilder
            .New(_connection, Context, Request, appDataView, childObjectType)
            .WithSource(ObjectType, id, breadcrumbs)
            .BuildAsync();

        return response;
    }

    private async Task<DataFormActionResponse> SaveDataViewAsync(SaveDataViewRequest request)
    {
        var query = _connection.Filter<AppDataView>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.ObjectType, ObjectType.FullName)
                .Eq(x => x.IsDefault, request.IsDefault)
                .Eq(x => x.Hash, request.Request.Hash)
            ;

        if (!request.IsDefault)
        {
            query.Eq(x => x.Name, request.Name);
        }

        if (request.ProfileIds?.Length > 0)
        {
            query.AnyIn(x => x.ProfileIds, request.ProfileIds);
        }
        else if (request.Role.HasValue)
        {
            query.Eq(x => x.Role, request.Role.Value);
        }
        else if (!request.IsDefault)
        {
            // current profile
            query.AnyEq(x => x.ProfileIds, Context.ProfileId.Value);
        }

        var appDataView = await query.FirstOrDefaultAsync();
        if (appDataView != null)
        {
            return new DataFormActionResponse
            {
                Success = false,
                Message = "There is already a view with this name for this object"
            };
        }

        // do not change the requested breakpoint so one can save a view calculated for a breakpoint in another 
        // if (request.Breakpoint.HasValue)
        // {
        //     request.Request.Breakpoint = request.Breakpoint.Value;
        // }

        // TODO: impersonate to limit fields? 
        // probably not needed as we will check when rendering the dataview 
        // ...

        appDataView = BuildDataView();

        appDataView.Criteria = request.Request.Criteria?.Length > 0
            ? new Criteria
            {
                Conditions = request.Request.Criteria,
            }
            : null;

        appDataView.Fields = request.Request.Fields;
        appDataView.OrderBy = request.Request.OrderBy;
        appDataView.Name = request.Name;
        appDataView.Description = request.Description ?? request.Name;
        appDataView.DataView.Title = request.Description ?? request.Name;
        appDataView.CreatedOn = DateTime.UtcNow;
        appDataView.LastActor = Context.Actor();
        appDataView.Hash = request.Request.Hash;
        appDataView.IsDefault = request.IsDefault;

        appDataView.Role = null;
        appDataView.ProfileIds = null;

        if (request.ProfileIds?.Length > 0)
        {
            appDataView.ProfileIds = request.ProfileIds;
        }
        else if (request.Role.HasValue)
        {
            appDataView.Role = request.Role.Value;
        }

        appDataView.IsActive = true;

        // set fields so it can calculate view options
        appDataView.DataView.Fields = request.Request.Fields
            .Select(x => ObjectType.Fields.TryGetValue(x, out var field) ? field.Field : null)
            .Where(x => x != null)
            .ToArray();

        appDataView.Options = appDataView.CalculateDataViewOptions(request.Request);

        // clear before saving (as it is not needed)
        appDataView.DataView.Fields = null;
        appDataView.StoredProcedure = null;

        if (!request.Breakpoint.HasValue)
        {
            // clear breakpoint so it will work for all 
            appDataView.Breakpoint = null;
        }

        appDataView = await _connection.InsertAsync(appDataView);

        // TODO: fire event?
        // ...

        return new DataFormActionResponse
        {
            // Action = $"datagrid://api/v1/AppDataView({appDataView.Id})",
            Action = $"{FormAction.Client_LoadView}={Uri.EscapeDataString(appDataView.Name)}",
            Success = true,
            Message = $"View {request.Name} saved",
        };
    }

    private async Task<IEnumerable<ReferenceValue>> LookupAsync(bool topValues = false)
    {
        if (topValues)
        {
            if (string.IsNullOrEmpty(Request.LookupField)) throw new BadRequestException("Missing Lookup Field");
        }
        else
        {
            Request.LookupField ??= ObjectType.LookupFields?.Key ?? Model.IdFieldName;
        }

        var keyFieldName = Request.LookupField;
        var nameFieldName = topValues ? keyFieldName : ObjectType?.LookupFields?.Name ?? nameof(Model.Name);
        var descriptionFieldName = topValues ? null : ObjectType?.LookupFields?.Description;
        var imageUrlFieldName = topValues ? null : ObjectType?.LookupFields?.ImageUrl;

        var dataView = await BuildDataViewAsync();

        if (topValues)
        {
            return dataView.Result?
                .Select(x =>
                {
                    var iDict = x as IDictionary<string, object>;
                    if (!iDict.TryGetStrParam("_id", out var nameValue) || string.IsNullOrEmpty(nameValue)) return null;

                    var description = iDict.TryGetParam("count", out var countValue) ? $"{nameValue} ({countValue})" : null;
                    return new ReferenceValue
                    {
                        Id = nameValue,
                        Value = nameValue,
                        Description = description,
                    };
                })
                .Where(x => x != null)
                .ToArray();
        }

        return dataView.Result?
            .Select(x =>
            {
                var iDict = x as IDictionary<string, object>;

                var name = iDict.TryGetParam(nameFieldName, out var nameValue) ? nameValue?.ToString() : null;
                var description = descriptionFieldName != null && iDict.TryGetParam(descriptionFieldName, out var descriptionValue) ? descriptionValue?.ToString() : null;
                var imageUrl = imageUrlFieldName != null && iDict.TryGetParam(imageUrlFieldName, out var imageUrlValue) ? imageUrlValue?.ToString() : null;

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    // TODO: replace fields in the template?
                    // ...
                }

                if (!iDict.TryGetParam(keyFieldName, out var keyFieldValue))
                {
                    _logger.LogInformation("Didn't find {Field} Value in the input for lookup", keyFieldName);
                    keyFieldValue = "";
                }

                return new ReferenceValue
                {
                    Id = keyFieldValue switch
                    {
                        string str => str,
                        ObjectId objectId => objectId.ToGuid().ToString(),
                        _ => keyFieldValue.ToString(),
                    },
                    Value = name,
                    Description = description,
                    ImageUrl = imageUrl,
                };
            })
            .ToArray();
    }

    private async Task<AppDataView> CreateDataViewAsync()
    {
        var appDataView = BuildDataView();

        appDataView.Criteria = Request.Criteria?.Length > 0
            ? new Criteria
            {
                Conditions = Request.Criteria,
            }
            : null;

        appDataView.Fields = Request.Fields;
        appDataView.OrderBy = Request.OrderBy;
        appDataView.Name = appDataView.Id.ToString("N");
        appDataView.CreatedOn = DateTime.UtcNow;
        appDataView.LastActor = Context.Actor();
        appDataView.ProfileIds =
        [
            Context.ProfileId.Value
        ];

        appDataView.IsActive = false;

        // set fields so it can calculate view options
        appDataView.DataView.Fields = Request.Fields
            .Select(x => ObjectType.Fields.TryGetValue(x, out var field) ? field.Field : null)
            .Where(x => x != null)
            .ToArray();

        appDataView.Options = appDataView.CalculateDataViewOptions(Request);

        // clear before saving (as it is not needed)
        appDataView.DataView.Fields = null;
        appDataView.StoredProcedure = null;

        appDataView = await _connection.InsertAsync(appDataView);

        // TODO: fire event?
        // ...

        return appDataView;
    }

    private static IEnumerable<ActionMenuItem> GetEditActions(IEntityContext context, ObjectType objectType)
    {
        yield return new ActionMenuItem
        {
            Name = "View",
            Visible = ["selectedCount=='1'"],
            Action = ObjectTypeService.BuildDataFormUrl(objectType, formName: FormName.View),
            // Icon = Icons.Edit,
        };

        if (objectType.CanCreate(context) && !objectType.IsEmbedded)
        {
            // TODO: check if there are subtypes
            // if (objectType.Discriminator?.Keys.Count > 0)
            // {
            //     // TODO: add menu instead    
            // }
            // else
            // {
            if (objectType.IsAbstract)
            {
                // ...

                // TODO: look for templates? 
                // ... 
            }
            else
            {
                yield return new ActionMenuItem
                {
                    Name = FormAction.Add,
                    Visible = ["selectedCount=='0'"],
                    Action = ObjectTypeService.BuildDataFormUrl(objectType, formName: FormName.Add),
                    Icon = nameof(Icons.Add),
                };
            }
            // }
        }

        if (objectType.CanUpdate(context))
        {
            yield return new ActionMenuItem
            {
                Name = FormAction.Edit,
                Visible = new[] { "selectedCount=='1'" },
                Action = ObjectTypeService.BuildDataFormUrl(objectType),
                // Icon = Icons.Edit,
            };
        }

        if (objectType.CanDelete(context))
        {
            yield return new ActionMenuItem
            {
                Name = FormAction.Delete,
                Visible = new[] { objectType.Can(context, ObjectTypePermission.BulkDelete) ? "selectedCount!='0'" : "selectedCount=='1'" },
                // TODO: add confirmation dialog instead of deleting directly?
                // ...
                // Action = $"dataForm://api/v1/CustomObject({objectType.Name})?action=Delete", // TODO: use GetDataFormUrl?
                Action = "dataForm://api/v1/CustomObject/" + objectType.FullName + "({{id}})?action=Delete", // TODO: use GetDataFormUrl?
            };
        }

        if (objectType.Can(context, ObjectTypePermission.Export))
        {
            yield return new ActionMenuItem
            {
                Name = "Download",
                Visible = new[] { "selectedCount=='0'" },
                Action = FormAction.Client_DonwloadCsv,
                Icon = nameof(Icons.Download),
            };
        }

        if (objectType.Can(context, ObjectTypePermission.Import))
        {
            yield return new ActionMenuItem
            {
                Name = "Upload",
                Visible = new[] { "selectedCount=='0'" },
                // Action = FormAction.Client_Upload,
                Action = $"dataForm://api/v1/ObjectType/{objectType.FullName}/Import",
                Icon = nameof(Icons.Upload),
            };
        }

        if (objectType.Can(context, ObjectTypePermission.BulkTag))
        {
            yield return new ActionMenuItem
            {
                Name = "Tag",
                Label = "Tag/Untag...",
                Action = $"dataForm://api/v1/Tag/{objectType.FullName}",
                Icon = nameof(Icons.Tag),
                Visible = new[] { "selectedCount!='0'" },
            };
        }
    }

    private async Task<DataViewResponse> BuildDataViewAsync(AppDataView appDataView)
    {
        if (appDataView.DataView.Menu == null && Projection != Projection.Lookup && Projection != Projection.TopValues)
        {
            var menuActions = new List<MenuItem>(GetEditActions(Context, ObjectType));
            if (menuActions.Count > 0)
            {
                appDataView.DataView.Menu = new Menu
                {
                    Name = "Edit",
                    Items = menuActions.ToArray(),
                };
            }
        }

        // if not stored procedure, create a placeholder, so it can pass down the collection name
        appDataView.StoredProcedure ??= new AggregateStoredProcedure
        {
            Collection = ObjectType.CollectionName,
            DatabaseName = ObjectType.DatabaseName,
        };

        Request.OrderBy ??= appDataView.OrderBy;
        Request.Fields ??= appDataView.Fields;

        var response = await BuildDataViewResponseAsync(appDataView);

        // augment response
        response.Id = appDataView.Id;
        // response.ObjectId = null;

        if (response.View.IsSelectable)
        {
            // supports selecting, so it can have actions
            var (allowNone, actions) = await _objectTypeService.GetUserActionsMenuItemsAsync(Context, ObjectType, appDataViewId: appDataView.Id);
            if (!actions.IsEmpty())
            {
                // add actions to menu (create menu if none)
                UserActionService.UpdateActionsMenu(response, actions.ToList(), allowNone);
            }

            if (response.View.Menu == null || response.View.Menu.Items.IsEmpty())
            {
                // no reason to allow selection if there are no actions to take on the items. 
                response.View.IsSelectable = false;
            }
        }

        var query = _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.ObjectType, ObjectType.FullName)
            .Ne(x => x.IsDefault, true)
            .In(x => x.Role, new[] { default(EntityRoleId?), Context.Role })
            .Ne(x => x.IsActive, false)
            .In(x => x.Breakpoint, new[] { default(ScreenBreakpoint?), Request.Breakpoint });

        if (Context.ProfileId.HasValue)
        {
            if (Context.AllProfileIds.Length > 1)
            {
                query.OrBuilder(
                    q => q.Eq(x => x.ProfileIds, null),
                    q => q.AnyIn(x => x.ProfileIds, Context.AllProfileIds)
                );
            }
            else
            {
                query.OrBuilder(
                    q => q.Eq(x => x.ProfileIds, null),
                    q => q.AnyEq(x => x.ProfileIds, Context.ProfileId.Value)
                );
            }
        }
        else
        {
            query.Eq(x => x.ProfileIds, null);
        }

        // views 
        var views = await query
            .SortAsc(x => x.Name)
            .FindAsync();

        if (Context.Role != EntityRoleId.Admin && views.Count <= 0) return response;

        response.View.Menu ??= new Menu
        {
            Name = "Main"
        };

        var viewMenu = new Menu
        {
            Name = "Views",
            Label = "Views",
            Icon = nameof(Icons.View),
            Items = getViewMenuItems().ToArray(),
            Visible = new[] { "selectedCount=='0'" }
        };

        response.View.Menu.Items = (response.View.Menu.Items ?? Enumerable.Empty<MenuItem>())
            .Append(viewMenu)
            .ToArray();

        // var currentView = views.FirstOrDefault(x => x.Name == request.View);
        // if (currentView != null && currentView.FlowId.HasValue)
        // {
        //     // get user actions for current view 
        //     var actions = await GetUserActionsAsync(context, nameof(AppDataView), currentView.Id);
        //     if (!actions.IsEmpty())
        //     {
        //         var actionsMenu = new Menu
        //         {
        //             Name = "View Actions",
        //             Items = actions.ToArray(),
        //             Collapsible = true,
        //         };
        //
        //         viewMenu.Items = viewMenu.Items.Append(actionsMenu).ToArray();
        //     }
        // }

        IEnumerable<MenuItem> getViewMenuItems()
        {
            foreach (var view in views)
            {
                yield return new ActionMenuItem
                {
                    Name = view.Name,
                    Label = view.Name == Request.View ? $"{view.Description ?? view.Name} (Current)" : view.Description ?? view.Name,
                    // Action = $"datagrid://api/v1/AppDataView({view.Id})"
                    Action = $"{FormAction.Client_LoadView}={Uri.EscapeDataString(view.Name)}",
                    Icon = view.Options switch
                    {
                        CardDataViewOptions => nameof(Icons.Card),
                        CalendarViewOptions => nameof(Icons.Calendar),
                        MapViewOptions => nameof(Icons.Map),
                        _ => nameof(Icons.Grid),
                    }
                };
            }

            // TODO: replace with some RBAC?
            if (Context.Role == EntityRoleId.Admin)
            {
                yield return new ActionMenuItem
                {
                    Name = "SaveView",
                    Action = FormAction.Client_Save,
                    Label = "Save View...",
                    Icon = nameof(Icons.Save),
                };
            }
        }

        // TODO: remove filter options for fields that are part of the appDataView.criteria?
        // ...

        return response;
    }

    private async ValueTask<AppDataView> LoadDataViewAsync(bool defaultView = false)
    {
        var query = _connection.Filter<AppDataView>()
                .Eq(x => x.AccountId, Context.AccountId)
                .In(x => x.Role, [default(EntityRoleId?), Context.Role])
                .Ne(x => x.IsActive, false)
                .Eq(x => x.ObjectType, ObjectType.FullName)
            ;

        if (defaultView)
        {
            // defaultView ? objectType.GetDefaultDataViewName(request.Hash) : 
            query
                .Eq(x => x.Hash, Request.Hash)
                .Eq(x => x.IsDefault, true);
        }
        else
        {
            query.Eq(x => x.Name, Request.View)
                .Ne(x => x.IsDefault, true);
        }

        if (Context.ProfileId.HasValue)
        {
            if (Context.AllProfileIds.Length > 1)
            {
                query.OrBuilder(
                    q => q.Eq(x => x.ProfileIds, null),
                    q => q.AnyIn(x => x.ProfileIds, Context.AllProfileIds)
                );
            }
            else
            {
                query.OrBuilder(
                    q => q.Eq(x => x.ProfileIds, null),
                    q => q.AnyEq(x => x.ProfileIds, Context.ProfileId.Value)
                );
            }
        }
        else
        {
            query.Eq(x => x.ProfileIds, null);
        }

        if (defaultView)
        {
            if (Request.Breakpoint.HasValue)
            {
                query.In(x => x.Breakpoint, new[] { null, Request.Breakpoint });
            }
            else
            {
                query.Eq(x => x.Breakpoint, null);
            }
        }

        var list = await query.FindAsync();

        if (list.Count < 2)
        {
            // 0 or 1 candidate
            return list.FirstOrDefault();
        }

        var filtered = list.ToArray();
        
        if (Context.ProfileId.HasValue)
        {
            if (Context.AllProfileIds.Length > 1)
            {
                // check profiles in order (first with any wins)
                foreach (var profileId in Context.AllProfileIds)
                {
                    // try to find exact match for profile
                    var candidates = filtered
                        .Where(x => x.ProfileIds?.Any(x => x == profileId) ?? false)
                        .ToArray();

                    if (candidates.Length == 1) return candidates[0];
                    if (candidates.Length > 1)
                    {
                        filtered = candidates;
                        break;
                    }
                }   
            }
            else
            {
                // try to find exact match for profile
                var candidates = filtered
                    .Where(x => x.ProfileIds?.Any(x => x == Context.ProfileId) ?? false)
                    .ToArray();

                if (candidates.Length == 1) return candidates[0];
                if (candidates.Length > 1) filtered = candidates;
            }
        }

        if (Request.Breakpoint.HasValue)
        {
            // try to find exact for breakpoint
            var candidates = list
                .Where(x => x.Breakpoint == Request.Breakpoint.Value)
                .ToArray();

            if (candidates.Length == 1) return candidates[0];
            if (candidates.Length > 1) filtered = candidates;
        }

        return filtered.FirstOrDefault();
    }

    private async Task<DataViewResponse> BuildDataViewAsync()
    {
        if (!string.IsNullOrEmpty(Request.View))
        {
            // load view
            var existingView = await LoadDataViewAsync();
            if (existingView == null)
            {
                return new DataViewResponse
                {
                    Request = Request,
                    Message = "Couldn't load view",
                };
            }

            Request.Fields ??= existingView.Fields;

            return await BuildDataViewAsync(existingView);
        }

        // TODO: could try to avoid having to load (again) the default view when loading a second page or when the user is saving settings
        // ...

        // build view 
        var appDataView = default(AppDataView);
        if (Request.Fields == null && !SkipCustomizations)
        {
            // try to load default view
            appDataView = await LoadDataViewAsync(true);
            // TODO: should it update Request.Fields?
            // ...
        }

        var cutOutDate = appDataView?.LastModifiedOn ?? appDataView?.CreatedOn;
        appDataView ??= BuildDataView();

        switch (Projection)
        {
            case Projection.Lookup:
            case Projection.TopValues:
                // lookup, no need/use for user settings
                return await BuildDataViewAsync(appDataView);
        }

        if (appDataView.Criteria?.Conditions?.Length > 0)
        {
            // view includes criteria
            // TODO: add support user settings ????
            // ...
            return await BuildDataViewAsync(appDataView);
        }

        if (!SkipCustomizations)
        {
            // handle user settings`
            await LoadOrUpdateUserSettingsAsync(appDataView, cutOutDate);
        }

        return await BuildDataViewAsync(appDataView);
    }

    /// <summary>
    /// Generate dataView from children of an object
    /// </summary>
    private async Task<DataViewResponse> GetChildrenDataViewAsync(Guid id, string expandPath)
    {
        // split expand path and recursively resolve path
        // ...

        var child = expandPath;

        // process child for indexes (e.g. Field[0], Field[Name=test], ...)
        // ...

        if (!ObjectType.Fields.TryGetValue(child, out var childField) || childField.Field is not ChildrenField childrenField) throw new BadRequestException($"Invalid path: {expandPath}");
        var childObjectType = await _introspector.GetObjectTypeAsync(childrenField.ChildrenFieldOptions?.ObjectType);
        if (childObjectType == null) throw new BadRequestException("Invalid object type configuration");

        // TODO: load/save user settings ???
        // ...

        var appDataView = await LoadDataViewAsync(true);
        appDataView ??= BuildDataView();

        if (appDataView.Criteria?.Conditions == null || appDataView.Criteria.Conditions.Length == 0)
        {
            // handle user settings 
            await LoadOrUpdateUserSettingsAsync(appDataView);
        }

        // change detail to be url to edit field using the "expandPath"
        // ... 
        appDataView.DataView.Detail = childrenField.ChildrenFieldOptions?.LinkUrl == null
            ? null
            : new DataViewDetail
            {
                Page = childrenField.ChildrenFieldOptions.LinkUrl,
            };

        // actions? 
        // "remove", "add", "edit", "view", ...
        // ...
        appDataView.DataView.Actions = null;
        appDataView.DataView.Menu = null;

        // selectable?
        // depends on the actions?
        // ...
        appDataView.DataView.IsSelectable = false;
        appDataView.DataView.Searchable = false;

        var breadcrumbs = new[]
        {
            new ChildrenObjectTypeDataViewResponseBuilder.Breadcrumb
            {
                Property = child,
                Field = childrenField
            }
        };

        var response = await BuildDataViewResponseAsync(id, appDataView, childObjectType, breadcrumbs);

        // augment response
        response.Id = appDataView.Id;
        // response.ObjectId = null;

        return response;
    }

    private AppDataView BuildDataView()
    {
        var appDataView = new AppDataView
        {
            Id = Guid.NewGuid(),
            AccountId = Context.AccountId.Value,
            Role = Context.Role,
            ObjectType = ObjectType.FullName,
            Name = ObjectType.GetDefaultDataViewName(Request.Hash),
            DataView = new DataView
            {
                Name = ObjectType.FullName,
                Title = ObjectType.LabelPlural ?? ObjectType.Label ?? ObjectType.Description ?? ObjectType.Name, // TODO: remove description from the list
                KeyField = "_id",
                PageSize = Request.Top > 0 ? Request.Top : 100,
                Searchable = ObjectType.IsFullTextSearchable,
                IsSelectable = true,
                Detail = new DataViewDetail
                {
                    Page = ObjectTypeService.BuildDataFormUrl(ObjectType, formName: FormName.View),
                },
                Fields = Array.Empty<FormField>(),
            },
            // Options = defaultOptions(),
            Breakpoint = Request.Breakpoint,
        };

        return appDataView;
    }

    private async Task LoadOrUpdateUserSettingsAsync(AppDataView appDataView, DateTime? cutOutDate = null)
    {
        if (Request.Fields?.Length > 0 && !cutOutDate.HasValue)
        {
            // save user settings 
            await UpsertUserSettingsForObjectTypeAsync(appDataView);
            return;
        }

        // try to load default for user
        await LoadUserSettingsForObjectAsync(appDataView, cutOutDate);
    }

    private async Task<ObjectTypeUserSettings> UpsertUserSettingsForObjectTypeAsync(AppDataView appDataView)
    {
        if (!Context.UserId.HasValue) return null; // || Projection != Projection.Fields

        return await _connection.Filter<ObjectTypeUserSettings>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.EntityId, Context.UserId.Value)
            .Eq(x => x.ObjectType, ObjectType.FullName)
            .Eq(x => x.Hash, Request.Hash)
            .Eq(x => x.Breakpoint, Request.Breakpoint)
            .Update
            .SetOnInsert(x => x.AccountId, Context.AccountId.Value)
            .SetOnInsert(x => x.EntityId, Context.UserId.Value)
            .SetOnInsert(x => x.ObjectType, ObjectType.FullName)
            .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
            .SetOnInsert(x => x.Hash, Request.Hash)
            .Set(x => x.Fields, Request.Fields)
            .Set(x => x.OrderBy, Request.OrderBy)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, Context.Actor())
            .UpdateAndGetOneAsync(true);
    }

    private FormField[] FilterReadableFields(AppDataView appDataView)
    {
        var readableFields = ObjectType.Fields
            .Where(x => x.Value.RBAC.CanRead(Context))
            .Where(x => x.Value.Field switch
            {
                CalculatedField => false,
                HiddenField => false,
                _ => true,
            })
            .ToDictionary(x => x.Key, x => x.Value.Field);

        // override the fields with the version in the dataView (if any)
        if (appDataView.DataView.Fields?.Length > 0)
        {
            foreach (var field in appDataView.DataView.Fields)
            {
                if (!readableFields.ContainsKey(field.Name)) continue;
                readableFields[field.Name] = field;
            }
        }

        return filteredFields().ToArray();

        IEnumerable<FormField> filteredFields()
        {
            foreach (var fieldName in Request.Fields)
            {
                if (Request.FixedFields?.Contains(fieldName) ?? false) continue;

                if (readableFields.TryGetValue(fieldName, out var field))
                {
                    yield return field;
                }
            }
        }
    }

    private async Task LoadUserSettingsForObjectAsync(AppDataView appDataView, DateTime? cutOutDate)
    {
        if (!Context.UserId.HasValue) return;

        var query = _connection.Filter<ObjectTypeUserSettings>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.EntityId, Context.UserId.Value)
            .Eq(x => x.ObjectType, ObjectType.FullName)
            .Eq(x => x.Hash, Request.Hash);

        if (Request.Breakpoint.HasValue)
        {
            query.In(x => x.Breakpoint, [Request.Breakpoint]) // null,
                .SortDesc(x => x.Breakpoint) // specific before default
                ;
        }
        else
        {
            // look for default 
            query.Eq(x => x.Breakpoint, null);
        }

        if (cutOutDate.HasValue)
        {
            // limit to settings saved after the view was created
            query.Gt(x => x.LastModifiedOn, cutOutDate);
        }

        var userSettings = await query.FirstOrDefaultAsync();
        if (userSettings == null) return;

        // override request
        Request.Fields = userSettings.Fields;
        Request.OrderBy = userSettings.OrderBy;

        // filter to make sure that user still have access to fields (probably enforced down the line anyway)
        appDataView.DataView.Fields = FilterReadableFields(appDataView);
    }
    
    /// <summary>
    /// Get Object matching what a filter would get for all visible fields
    /// in theory should match what you get with the ObjectTypeService, but it is a completely different way to get it
    /// </summary>
    public async Task<ExpandoObject> GetObjectAsync(IEntityContext context, ObjectType objectType, Guid objectId, bool useApiNames = false)
    {
        UseApiNames = useApiNames;
        IncludeHiddenFields = false;
        IncludeAllFields = true;
        AutoGenerateReferenceFieldNames = false;
        SkipCustomizations = true;

        var list = await BuildResultSetAsync(context, objectType, new DataViewRequest
        {
            Top = 1,
            Criteria =
            [
                Condition.Eq(Model.IdFieldName, objectId)
            ],
            OrderBy = $"-{nameof(RoomSelection.CreatedOn)}", // TODO: should it be the api name instead?
        });

        return list.FirstOrDefault();
    }
}