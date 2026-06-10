using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Layout;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Route("/api/v1/[controller]")]
public class CustomObjectController : APIController
{
    private readonly ILogger<CustomObjectController> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public CustomObjectController(ILogger<CustomObjectController> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    /// <summary>
    /// Get all "readable" properties
    /// </summary>
    [Authorize("default")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})")]
    public async Task<Dictionary<string, object>> GetObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        var flatObject = await _objectTypeService.GetFlatObjectAsync(Context, objectType, objectId);
        if (flatObject == null) throw new NotFoundException($"{objectTypeName} not found");
        return flatObject;
    }

    [Authorize("default")]
    [HttpPost("/api/v1/[controller]({id:guid})/DataView")]
    [HttpPost("{objectTypeName}/DataView")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<IDataViewResponse> DataViewAsync([FromRoute] Guid? id, [FromRoute] string objectTypeName, [FromBody] DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        Prepare(request);

        var objectType = id.HasValue ? await _objectTypeService.GetAsync(Context, id.Value) : await _objectTypeService.GetAsync(Context, objectTypeName);

        if (objectType == null) throw new NotFoundException(nameof(ObjectType), id);

        var response = await builder.BuildDataViewAsync(Context, objectType, request);

        return response;
    }

    /// <summary>
    /// Access property in object that is an array (or dictionary) of objects
    /// only using object type name
    /// </summary>
    [Obsolete("nice idea but...")]
    [Authorize("default")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId:guid})/{expandPath}/DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<IDataViewResponse> DataViewChildrenByNameAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] string expandPath, [FromBody] DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        Prepare(request);

        request.Hash ??= $"Embedded({objectTypeName}.{expandPath})";

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        var resp = await builder.GetChildrenDataViewAsync(Context, objectType, objectId, request, expandPath);

        return resp;
    }

    /// <summary>
    /// Get form to save dataView
    /// accepts id or objectTypeName
    /// </summary>
    [HttpGet("{objectTypeName}/DataView/Save/DataForm")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/DataView/Save/DataForm")]
    [HttpGet("/api/v1/[controller]({id:guid})/DataView/Save/DataForm")]
    public async Task<Form> GetFormToSaveDataView([FromRoute] Guid? id, [FromRoute] string objectTypeName, [FromQuery] string breakpoint)
    {
        await Task.CompletedTask;

        return new Form
        {
            Name = "SaveView",
            Title = "Save View",
            Fields = new FormField[]
            {
                new CheckboxField
                {
                    Name = nameof(SaveDataViewRequest.IsDefault),
                    Label = "Default for Role/Profile(s)",
                },
                new TextField
                {
                    Name = nameof(SaveDataViewRequest.Name),
                    IsRequired = true,
                    // Visible = new[] { $"!{nameof(SaveDataViewRequest.IsDefault)}" }
                },
                new TextField
                {
                    Name = nameof(SaveDataViewRequest.Description),
                    IsRequired = false,
                    TextFieldOptions = new TextFieldOptions
                    {
                        Multline = true,
                    },
                },
                new MultiReferenceField
                {
                    Name = nameof(SaveDataViewRequest.ProfileIds),
                    Label = "Profile",
                    MultiReferenceFieldOptions = new MultiReferenceFieldOptions
                    {
                        ObjectType = nameof(AppProfile),
                    },
                    Visible = new[] { $"!{nameof(AppProfileElement.Role)}" },
                    DefaultValue = Context.ProfileId.HasValue ? new[] { Context.ProfileId.Value } : null,
                },
                new SelectField
                {
                    Name = nameof(SaveDataViewRequest.Role),
                    Label = "User Role",
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = new Dictionary<string, string>
                        {
                            { nameof(EntityRoleId.Admin), nameof(EntityRoleId.Admin) },
                            { nameof(EntityRoleId.Manager), nameof(EntityRoleId.Manager) },
                            { nameof(EntityRoleId.User), nameof(EntityRoleId.User) },
                        }
                    },
                    Visible = new[] { $"!{nameof(AppProfileElement.ProfileIds)}" },
                },
                new SelectField
                {
                    Name = nameof(SaveDataViewRequest.Breakpoint),
                    Label = "Breakpoint",
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = new Dictionary<string, string>
                        {
                            { nameof(ScreenBreakpoint.ExtraSmall), "Extra Small" },
                            { nameof(ScreenBreakpoint.Small), "Small" },
                            { nameof(ScreenBreakpoint.Medium), "Medium" },
                            { nameof(ScreenBreakpoint.Large), "Large" },
                            { nameof(ScreenBreakpoint.ExtraLarge), "Extra Large" },
                        }
                    },
                    // stupid: it can't parse back automatically
                    // TODO: there must be a better way 
                    DefaultValue = breakpoint switch
                    {
                        "xs" => nameof(ScreenBreakpoint.ExtraSmall),
                        "sm" => nameof(ScreenBreakpoint.Small),
                        "md" => nameof(ScreenBreakpoint.Medium),
                        "lg" => nameof(ScreenBreakpoint.Large),
                        "xl" => nameof(ScreenBreakpoint.ExtraLarge),
                        _ => breakpoint,
                    },
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = FormAction.Client_Cancel,
                    Label = "Cancel",
                    Action = FormAction.Client_Cancel,
                },
                new FormAction
                {
                    Name = FormAction.Client_Save,
                    Label = "Save",
                    Action = FormAction.Client_Save,
                },
            }
        };
    }

    /// <summary>
    /// Save view
    /// </summary>
    [Authorize("admin")]
    [HttpPost("/api/v1/[controller]({id:guid})/DataView/Save")]
    [HttpPost("{objectTypeName}/DataView/Save")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/DataView/Save")]
    [Produces("text/csv", "application/json")]
    public async Task<DataFormActionResponse> SaveDataViewAsync([FromRoute] Guid? id, [FromRoute] string objectTypeName, [FromBody] SaveDataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var objectType = id.HasValue ? await _objectTypeService.GetAsync(Context, id.Value) : await _objectTypeService.GetAsync(Context, objectTypeName);

        if (objectType == null) throw new NotFoundException(nameof(ObjectType), id);
        var response = await builder.SaveDataViewAsync(Context, objectType, request);
        return response;
    }

    [Obsolete("use objectType controller")]
    [Authorize("managerplus")]
    [HttpPost("/api/v1/[controller]({id:guid})/Import")]
    [HttpPost("{objectTypeName}/Import")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Import")]
    [Consumes("application/octet-stream", "multipart/form-data")]
    public async Task<DataFormActionResponse> DataViewImportByIdAsync([FromRoute] Guid? id, [FromRoute] string objectTypeName, IFormFile file)
    {
        var objectType = id.HasValue ? await _objectTypeService.GetAsync(Context, id.Value) : await _objectTypeService.GetAsync(Context, objectTypeName);

        if (objectType == null) throw new NotFoundException(nameof(ObjectType), id);
        return await _objectTypeService.ImportCsvAsync(Context, objectType, file.OpenReadStream());
    }

    [Authorize("default")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Lookup")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/Lookup")]
    [HttpPost("/api/v1/[controller]({id::guid})/Lookup")]
    public async Task<IEnumerable<ReferenceValue>> LookupByIdAsync([FromRoute] Guid? id, [FromRoute] string objectTypeName, DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var objectType = id.HasValue ? await _objectTypeService.GetAsync(Context, id.Value) : await _objectTypeService.GetAsync(Context, objectTypeName);

        var condition = request.Criteria?.FirstOrDefault(x => x.FieldName == Condition.LookupId);

        if (objectType == null) throw new NotFoundException($"{objectTypeName}: {condition?.Value}");
        return await builder.LookupAsync(Context, objectType, request);
    }
    
    /// <summary>
    /// Top matching values for a field 
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Field({fieldName})/Top/Lookup")]
    [HttpPost("/api/v1/[controller]/{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/Field({fieldName})/Top/Lookup")]
    [HttpPost("/api/v1/[controller]({id::guid})/Field({fieldName})/Top/Lookup")]
    public async Task<IEnumerable<ReferenceValue>> TopValuesForFieldAsync([FromRoute] Guid? id, [FromRoute] string objectTypeName, [FromRoute] string fieldName, DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var objectType = id.HasValue ? await _objectTypeService.GetAsync(Context, id.Value) : await _objectTypeService.GetAsync(Context, objectTypeName);
        
        // TODO: check conditions
        // if it is a "#id" (initial lookup) just return it as the only option
        // ... 
        
        var condition = request.Criteria?.FirstOrDefault(x => x.FieldName == Condition.AutoComplete);
        if (objectType == null) throw new NotFoundException($"{objectTypeName}: {condition?.Value}");
        
        request.LookupField = fieldName;
        // request.Criteria = condition?.Value!=null ? 
        // [
        //     Condition.Eq(fieldName, condition.Value)
        // ] : [];
        
        return await builder.TopValuesAsync(Context, objectType, request);
    }

    /// <summary>
    /// Get "Save layouts" for form (id in query or as part of the route) 
    /// </summary>
    [Authorize("admin")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/Layout/Save/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{formName}/Layout/Save/DataForm")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Layout/Save/DataForm")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/{formName}/Layout/Save/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/Layout/Save/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{formName}/Layout/Save/DataForm")]
    public Form GetSaveLayoutForm([FromRoute] string objectTypeName, [FromRoute] FormName formName = FormName.Edit)
    {
        return new Form
        {
            Name = "SaveLayouts",
            Title = "Save Layouts",
            Fields = new FormField[]
            {
                new ReferenceField
                {
                    Name = nameof(AppFormLayout.ObjectType),
                    Label = "Object Type",
                    ReferenceFieldOptions = new ReferenceFieldOptions
                    {
                        ObjectType = nameof(ObjectType),
                        ForeignFieldName = nameof(ObjectType.FullName),
                    },
                    DefaultValue = objectTypeName,
                },
                new MultiReferenceField
                {
                    Name = nameof(AppProfileElement.ProfileIds),
                    Label = "Profile",
                    MultiReferenceFieldOptions = new MultiReferenceFieldOptions
                    {
                        ObjectType = nameof(AppProfile),
                    },
                    Visible = new string[] { $"!{nameof(AppProfileElement.Role)}" },
                },
                new SelectField
                {
                    Name = nameof(AppProfileElement.Role),
                    Label = "User Role",
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = new Dictionary<string, string>
                        {
                            { nameof(EntityRoleId.Admin), nameof(EntityRoleId.Admin) },
                            { nameof(EntityRoleId.Manager), nameof(EntityRoleId.Manager) },
                            { nameof(EntityRoleId.User), nameof(EntityRoleId.User) },
                        }
                    },
                    Visible = new string[] { $"!{nameof(AppProfileElement.ProfileIds)}" },
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = FormAction.Client_Cancel,
                    Label = "Cancel",
                    Action = FormAction.Client_Cancel,
                },
                new FormAction
                {
                    Name = FormAction.Client_Save,
                    Label = "Save",
                    Action = FormAction.Client_Save,
                },
            }
        };
    }

    [Authorize("admin")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/Layout/Save")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{formName}/Layout/Save")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Layout/Save")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/{formName}/DataForm/Layout/Save")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/Layout/Save")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{formName}/Layout/Save")]
    public async Task<BreakpointLayouts> SaveLayoutAsync([FromRoute] string objectTypeName, [FromBody] SaveFormLayoutsRequest request, [FromRoute] FormName formName = FormName.Edit)
    {
        // TODO: other validation/adjustments
        // ...

        foreach (var breakPointLayout in request.Layouts.All.OfType<GridFormLayout>())
        {
            foreach (var row in breakPointLayout.Rows)
            {
                foreach (var cell in row.Fields)
                {
                    cell.Width = 12 / row.Fields.Length;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ObjectType) && request.ObjectType != objectTypeName)
        {
            var objectType = await _objectTypeService.GetAsync(Context, request.ObjectType);
            if (objectType == null) throw NotFoundException.New("ObjectType");
            if (!objectType.GetLoadedBaseObjectTypeNames().Contains(objectTypeName)) throw new BadRequestException($"{objectType.FullName} does not extend {objectTypeName}");
            objectTypeName = objectType.FullName;
        }

        // TODO: get flowid, objectstatusid
        // ... 

        var layout = new AppFormLayout
        {
            AccountId = Context.AccountId.Value,
            CreatedOn = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            LastActor = Context.Actor,
            Layouts = request.Layouts,
            Name = request.Name ?? $"{objectTypeName}: {formName}",
            Description = request.Description ?? $"{formName} for {objectTypeName}",
            ProfileIds = request.ProfileIds,
            Role = request.Role,
            ObjectType = objectTypeName,
            FormName = formName.ToString(),
            IsActive = true,
        };

        layout = await _connection.InsertAsync(layout);

        if (layout != null)
        {
            // replace any other (active) layouts 
            var query = _connection.Filter<AppFormLayout>()
                    .Eq(x => x.AccountId, layout.AccountId)
                    .Eq(x => x.ObjectType, layout.ObjectType)
                    .Eq(x => x.FormName, layout.FormName)
                    .Ne(x => x.Id, layout.Id)
                    .Ne(x => x.IsActive, false)
                ;

            if (layout.ProfileIds?.Length > 0)
            {
                query.All(x => x.ProfileIds, layout.ProfileIds);
            }
            else if (layout.Role.HasValue)
            {
                query.Eq(x => x.Role, layout.Role);
            }

            await query.Update
                .Set(x => x.IsActive, false)
                .Set(x => x.ReplacedById, layout.Id)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, Context.Actor)
                .UpdateManyAsync();
        }

        // TODO: fire event 
        // ...

        return layout.Layouts;
    }

    /// <summary>
    /// Add Object using template 
    /// </summary>
    [Authorize("default")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Add/Template/DataForm")]
    public async Task<Form> GetSelectTemplateFormAsync([FromRoute] string objectTypeName)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) return Form.BuildErrorForm($"{objectTypeName} not found", $"Add {objectTypeName}");

        var templates = await _connection.GetProfileElementQuery<TemplateObject>(
            Context,
            q => q
                .Eq(x => x.ObjectType, objectType.FullName)
                .Ne(x => x.IsActive, false)
        ).FindAsync();

        if (templates.Count < 1)
        {
            return Form.BuildErrorForm("No Templates Available", $"Add {objectType.Description ?? objectType.Name}");
        }

        return new Form
        {
            Name = "PickTemplate",
            Title = $"Add {objectType.Description ?? objectType.Name}",
            Fields = new FormField[]
            {
                new SelectField
                {
                    Name = "TemplateObjectId",
                    Label = "Select Template",
                    IsRequired = true,
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = templates.OrderBy(x => x.Description ?? x.Name).ToDictionary(x => x.Id.ToString(), x => x.Description ?? x.Name),
                    }
                }
            },
            Actions = new FormAction[]
            {
                new()
                {
                    Label = "Next",
                    Action = "dataform:/api/v1/CustomObject(" + objectType.FullName + ")/Add/Template({{TemplateObjectId}})",
                    Enable = new[] { Form.RequiredFieldsName }
                }
            }
        };
    }

    /// <summary>
    /// Add Object using template: get form 
    /// </summary>
    [Authorize("default")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Add/Template({templateId})/DataForm")]
    public async Task<Form> GetAddFormUsingTemplateAsync([FromRoute] string objectTypeName, [FromRoute] Guid templateId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) return Form.BuildErrorForm($"{objectTypeName} not found", $"Add {objectTypeName}");

        if (!objectType.RBAC.Can(Context, ObjectTypePermission.Create)) throw new ForbiddenException(Context, "Create");

        var template = await _connection.GetProfileElementQuery<TemplateObject>(
            Context,
            q => q
                .Eq(x => x.ObjectType, objectType.FullName)
                .Ne(x => x.IsActive, false)
                .Eq(x => x.Id, templateId)
        ).FirstOrDefaultAsync();

        if (template == null) return Form.BuildErrorForm($"Template not found", $"Add {objectTypeName}");

        objectType = await _objectTypeService.ResolveSubTypeAsync(Context, objectType, template.Object);
        var form = await _objectTypeService.BuildAddFormAsync(Context, objectType, template);

        return form;
    }

    /// <summary>
    /// Add Object using template 
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Add/Template({templateId})/DataForm")]
    public async Task<DataFormActionResponse> AddFormUsingTemplateAsync([FromRoute] string objectTypeName, [FromRoute] Guid templateId, [FromBody] DataFormActionRequest request)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) return new DataFormActionResponse(request, $"{objectTypeName} not found");

        if (!objectType.RBAC.Can(Context, ObjectTypePermission.Create)) throw new ForbiddenException(Context, "Create");

        var template = await _connection.GetProfileElementQuery<TemplateObject>(
            Context,
            q => q
                .Eq(x => x.ObjectType, objectType.FullName)
                .Ne(x => x.IsActive, false)
                .Eq(x => x.Id, templateId)
        ).FirstOrDefaultAsync();

        if (template == null) return new DataFormActionResponse(request, $"Template not found");

        objectType = await _objectTypeService.ResolveSubTypeAsync(Context, objectType, template.Object);

        var result = await _objectTypeService.ExecAddObjectAsync(Context, objectType, request, template);
        return result;
    }

    /// <summary>
    /// Edit Object (id in query or as part of the route) 
    /// </summary>
    [Authorize("default")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{formName}/DataForm")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/DataForm")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/{formName}/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{formName}/DataForm")]
    public Task<Form> GetEditFormAsync([FromRoute] string objectTypeName, [FromQuery] string id, [FromRoute] string objectId, [FromRoute] FormName formName = FormName.Edit)
    {
        return GetDataFormAsync(Context, objectTypeName, objectId ?? id, formName);
    }

    /// <summary>
    /// Upsert Object: get form 
    /// </summary>
    [Authorize("default")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-za-z_0-9\\.]]+$)})/Upsert/DataForm")]
    public async Task<Form> GetUpsertFormAsync([FromRoute] string objectTypeName)
    {
        var args = Request.Query.ToDictionary(arg => arg.Key, arg => (object)arg.Value.FirstOrDefault());
        return await _objectTypeService.GetUpsertFormAsync(Context, objectTypeName, args);
    }

    /// <summary>
    /// Upsert Object 
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-za-z_0-9\\.]]+$)})/Upsert/DataForm")]
    public async Task<DataFormActionResponse> UpsertFormOnActionAsync([FromRoute] string objectTypeName, [FromBody] DataFormActionRequest request)
    {
        if (request.TryGetGuidParam(Model.IdFieldName, out var objectId))
        {
            request.SelectedIds =
            [
                objectId
            ];
        }

        var result = await _objectTypeService.ExecObjectActionAsync(Context, objectTypeName, request);

        return result;

        // if (!result.Success) return result;
        //
        // return new DataFormActionResponse(request)
        // {
        //     Action = result.Action,
        //     Ids = result.Ids,
        //     Success = true,
        //     RunId = result.RunId,
        //     NextUrl = FormAction.Client_Reload,
        //     Message = result.Message,
        // };
    }

    /// <summary>
    /// Simulate Form for Object/(Profile or Role) (id in query or as part of the route) 
    /// </summary>
    [Authorize("admin")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/Profile({roleOrProfileId})/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{formName}/Profile({roleOrProfileId})/DataForm")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Profile({roleOrProfileId})/DataForm")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/{formName}/Profile({roleOrProfileId})/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/Profile({roleOrProfileId})/DataForm")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{formName}/Profile({roleOrProfileId})/DataForm")]
    public async Task<Form> SimulateDataFormAsync(
        [FromRoute] string objectTypeName,
        [FromQuery] string id,
        [FromRoute] string objectId,
        [FromRoute] string roleOrProfileId,
        [FromRoute] FormName formName = FormName.Edit)
    {
        var context = default(IEntityContext);
        if (Guid.TryParse(roleOrProfileId, out var profileId))
        {
            var profile = await _connection.Filter<AppProfile>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, profileId)
                .FirstOrDefaultAsync();

            if (profile == null) throw new ForbiddenException("profile");

            context = ProfileContext.Create(profile.Id, Context.AccountId.Value, Context.UserId.Value, Context.ClientId).WithActorFrom(Context);
        }
        else if (Enum.TryParse<EntityRoleId>(roleOrProfileId, out var roleId))
        {
            context = UserContext.OrgUser(Context.UserId.Value, "", roleId, Guid.Empty, Context.AccountId.Value, Context.ClientId);
        }

        return await GetDataFormAsync(context, objectTypeName, objectId ?? id, formName);
    }

    /// <summary>
    /// Get Profiles that have access to the "form"
    /// </summary>
    [Authorize("admin")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/Profile")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{formName}/Profile")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Profile")]
    [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/{formName}/Profile")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/Profile")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{formName}/Profile")]
    public async Task<IEnumerable<ReferenceValue>> GetProfilesForFormAsync(
        [FromRoute] string objectTypeName,
        [FromQuery] string id,
        [FromRoute] string objectId,
        [FromRoute] FormName formName = FormName.Edit)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new ForbiddenException();

        objectId ??= id;
        if (string.IsNullOrWhiteSpace(objectId))
        {
            formName = FormName.Add;
        }

        var result = new List<ReferenceValue>
        {
            new()
            {
                Id = nameof(EntityRoleId.Manager),
                Value = "Role: Manager",
            },
            new()
            {
                Id = nameof(EntityRoleId.User),
                Value = "Role: User",
            },
        };

        var profileIds = getProfileIds().ToArray();
        if (profileIds.Length > 0)
        {
            var list = await _connection.Filter<AppProfile>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .In(x => x.Id, profileIds)
                .FindAsync();

            result.AddRange(list.Select(x => new ReferenceValue
            {
                Id = x.Id.ToString(),
                Value = x.Name,
                Description = x.Description,
            }));
        }

        return result;

        IEnumerable<Guid> getProfileIds()
        {
            if (objectType.RBAC?.Permissions == null) yield break;

            foreach (var kvp in objectType.RBAC.Permissions)
            {
                var canAccess = formName switch
                {
                    FormName.Add => kvp.Value.HasFlag(ObjectTypePermission.Create),
                    FormName.Edit => kvp.Value.HasFlag(ObjectTypePermission.Update),
                    FormName.View => kvp.Value.HasFlag(ObjectTypePermission.Read),
                    FormName.Details => kvp.Value.HasFlag(ObjectTypePermission.Read),
                    _ => false,
                };

                if (canAccess && Guid.TryParse(kvp.Key, out var profileId))
                {
                    yield return profileId;
                }
            }
        }
    }

    private async Task<Form> GetDataFormAsync(IEntityContext context, string objectTypeName, string objectId, FormName formName)
    {
        var objectType = await _objectTypeService.GetAsync(context, objectTypeName);
        return await GetDataFormAsync(context, objectType, objectId, formName);
    }

    private async Task<Form> GetDataFormAsync(IEntityContext context, ObjectType objectType, string objectId, FormName formName)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            formName = FormName.Add;
        }

        if (string.IsNullOrEmpty(objectId) || formName == FormName.Add)
        {
            var addForm = await _objectTypeService.GetAddDataFormAsync(context, objectType);
            return SetDefaultValues(addForm, formName);
        }

        // load object using "Admin context"
        var dynamicRecord = default(ExpandoObject);
        if (Guid.TryParse(objectId, out var id))
        {
            dynamicRecord = await _objectTypeService.GetExpandoObjectByIdAsync(Context, objectType, id);
        }
        else if (objectType.UniqueExternalId)
        {
            // external id?
            dynamicRecord = await _objectTypeService.GetExpandoObjectByExternalIdAsync(Context, objectType, objectId);
            var record = (IDictionary<string, object>)dynamicRecord;
            if (!record.TryGetGuidParam("_id", out var guid))
            {
                throw new NotFoundException("Couldn't determine id field");
            }

            id = guid;
        }

        if (dynamicRecord == null) throw new NotFoundException($"{objectType.FullName} not found");

        var updateForm = await _objectTypeService.GetDataFormForObjectAsync(context, objectType, id, dynamicRecord, formName);
        return SetDefaultValues(updateForm, formName);
    }

    private Form SetDefaultValues(Form form, FormName formName, bool disableFields = false)
    {
        if (form == null) throw new NotFoundException();

        var fields = form.Fields.ToDictionary(x => x.Name);
        foreach (var query in Request.Query)
        {
            if (!fields.TryGetValue(query.Key, out var field)) continue;
            if (field.IsReadOnly) continue;

            if (formName != FormName.Add && field.DefaultValue != null) continue;
            
            // field.DefaultValue = field.AutoConvert(query.Value.FirstOrDefault());
            object value = query.Value.Count > 1 ? query.Value.Select(object (x) => x).ToArray() : query.Value.FirstOrDefault();
            field.DefaultValue = field.AutoConvert(value); 
            
            if (disableFields) field.Enable = ["false"];
        }

        return form;
    }

    /// <summary>
    /// Execute action (id was in query or route)
    /// </summary>
    [Authorize("default")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/DataForm")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/{formName}/DataForm")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/DataForm")]
    [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/{formName}/DataForm")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/DataForm")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/{formName}/DataForm")]
    public async Task<DataFormActionResponse> EditFormOnActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid? objectId, [FromBody] DataFormActionRequest request)
    {
        if (objectId.HasValue)
        {
            request.SelectedIds = new[]
            {
                objectId.Value,
            };
        }

        var result = await _objectTypeService.ExecObjectActionAsync(Context, objectTypeName, request);
        if (result == null) throw new NotFoundException();

        return result;
    }

    /// <summary>
    /// Execute action (id was in query or route)
    /// TODO: just make Clone a new FormName and create a "form" with all the fields that can be added/modified
    /// with the default value for fields based on the source/initial/constraint, ...
    /// ...
    /// </summary>
    [Authorize("default")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/Clone/DataForm")]
    public async Task<DataFormActionResponse> CloneActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromBody] DataFormActionRequest request)
    {
        request.SelectedIds = new[]
        {
            objectId,
        };

        request.Action = "Clone";

        var result = await _objectTypeService.ExecObjectActionAsync(Context, objectTypeName, request);
        if (result == null) throw new NotFoundException();

        return result;
    }

    [Authorize("default")]
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/Fields/Lookup")]
    public async Task<IEnumerable<ReferenceValue>> LookupFieldsForObjectTypeAsync([FromRoute] string objectTypeName, DataViewRequest request)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null)
        {
            return Enumerable.Empty<ReferenceValue>();
        }

        var fields = objectType.Fields
            .Where(x => x.Value.RBAC.CanRead(Context))
            .Select(x => new ReferenceValue
            {
                Id = x.Key,
                Value = x.Value.Field?.Description ?? x.Value.Field?.Name ?? x.Key,
            })
            .OrderBy(x => x.Value);

        var value = request.Criteria?.FirstOrDefault(x => x.FieldName == Condition.AutoComplete)?.Value.ToString();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return fields.Where(x => x.Id.Contains(value));
        }

        return fields;
    }

    [Obsolete("use tag controller")]
    [Authorize("default")]
    [HttpPost("Tags({id::guid})/Lookup")]
    [HttpPost("Tags({objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)})/Lookup")]
    public async Task<IEnumerable<ReferenceValue>> LookupByIdAsync([FromRoute] Guid? id, [FromRoute] string objectTypeName, DataViewRequest request, [FromServices] ObjectTypeService objectTypeService)
    {
        var objectType = id.HasValue ? await _objectTypeService.GetAsync(Context, id.Value) : await _objectTypeService.GetAsync(Context, objectTypeName);

        if (objectType == null) throw new NotFoundException(nameof(ObjectType), id);

        return await objectTypeService.LookupTagsAsync(Context, objectType, request);
    }

    /// <summary>
    /// Get Page for object
    /// - ProjectController "hides" this action for project (FIX IT)
    /// </summary>
    [Authorize("default")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({id})/DataPage")]
    public async Task<LayoutPage> GetObjectPageAsync([FromRoute] string objectTypeName, [FromRoute] Guid id)
    {
        var page = await GetPageAsync(objectTypeName, id);
        if (page != null) return page;

        var result = await _objectTypeService.BuildLayoutPageAsync(Context, objectTypeName, id);
        if (result == null) throw new NotFoundException();

        return result;
    }
    
    [Authorize("default")]
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({id})/{name}/DataPage")]
    public async Task<LayoutPage> GetPageForObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid id, [FromRoute] string name)
    {
        var page = await GetPageAsync(objectTypeName, id, name);
        if (page != null) return page;

        throw new NotFoundException();
    }

    private async Task<LayoutPage> GetPageAsync(string objectTypeName, Guid objectId, string name = null)
    {
        var page = await _connection.GetProfileElementAsync<AppPage>(Context, q =>
            q.Eq(x => x.ObjectType, objectTypeName)
                .Eq(x => x.Name, name ?? objectTypeName));

        if (page?.Page is not LayoutPage layoutPage)
        {
            return null;
        }
        
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        
        // TODO: little overkill to load/parse the entire object since most of the time all that we will want is the id
        //      but... 
        var expandoObj = await _objectTypeService.GetExpandoObjectByIdAsync(Context, objectType, objectId);
        var obj = await _objectTypeService.RecursivelyFlattenAsync(Context, objectType, expandoObj);
        
        var objectsContext = new Dictionary<string, object>
        {
            { "Object", obj }
        };

        if (!layoutPage.FillPlaceholders(Context, objectsContext))
        {
            _logger.LogError("Failed filling placeholders for {ObjectType}/{Page}", objectTypeName, name);
            return null;
        }
        
        return layoutPage;
    }
}