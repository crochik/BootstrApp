using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Route("/api/v1/[controller]")]
public partial class ObjectTypeController : APIController
{
    private readonly ILogger<ObjectTypeController> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public ObjectTypeController(
        ILogger<ObjectTypeController> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService
    )
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    [Authorize("admin")]
    [HttpGet("{objectType}")]
    public async Task<ObjectType> GetAsync([FromRoute] string objectType, [FromQuery] string @namespace, [FromQuery] bool? loadBaseObject)
    {
        var o = await _objectTypeService.GetAsync(Context, objectType, new GetObjectOptions(@namespace)
        {
            LoadBaseObject = loadBaseObject ?? true,
        });

        if (o == null) throw new NotFoundException($"{objectType} not found");

        return o;
    }

    /// <summary>
    /// Get import form for object type (to get the file)
    /// </summary>
    [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Import/DataForm")]
    [Authorize("managerplus")]
    public Form GetImportForm([FromRoute] string objectTypeName)
    {
        return new Form
        {
            Name = $"{objectTypeName}_Tags",
            Title = "Import...",
            Fields = new FormField[]
            {
                new FileField
                {
                    Name = "File",
                    IsRequired = true,
                    FileFieldOptions = new FileFieldOptions
                    {
                        ContentTypes = new[]
                        {
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            "application/vnd.ms-excel",
                            "text/csv"
                        }
                    }
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Next",
                    Label = "Next"
                }
            }
        };
    }

    /// <summary>
    /// upload file, and redirect to form to map fields
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Import/DataForm")]
    [Consumes("application/octet-stream", "multipart/form-data")]
    [Authorize("managerplus")]
    public async Task<DataFormActionResponse> ImportAsync([FromRoute] string objectTypeName, IFormFile file, [FromServices] RemoteFileService remoteFileService)
    {
        var objectType = await _objectTypeService.GetAsync<ObjectTypeWithImportOptions>(Context, objectTypeName);
        if (objectType == null) throw NotFoundException.New(objectTypeName);
        if (objectType.ImportOptions == null || !objectType.Can(Context, ObjectTypePermission.Import)) throw new ForbiddenException(Context, $"Import {objectTypeName}");

        var folder = await _connection.Filter<RemoteFolder>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, objectType.ImportOptions.TempRemoteFolderId)
            .FirstOrDefaultAsync();

        if (folder == null) throw NotFoundException.New<RemoteFolder>(objectType.ImportOptions.TempRemoteFolderId);

        var remoteFile = await remoteFileService.UploadAsync(Context, folder, file, objectType.ImportOptions.TempUploadFileOptions, new Dictionary<string, object>
        {
            { "ObjectType", objectTypeName },
        });

        remoteFile.EntityId = Context.UserId.Value;

        await _connection.InsertAsync(remoteFile);

        await _objectTypeService.FireCreateEventAsync(Context, remoteFile, e =>
        {
            e.Description ??= $"{file.FileName} Uploaded to {objectTypeName}";
            e.Action ??= "ObjectCreated";
            e.TryAddMetaValue(nameof(PI.Shared.Models.ObjectType), objectTypeName);
        });

        return new DataFormActionResponse
        {
            Success = true,
            NextUrl = $"dataForm://api/v1/ObjectType/{objectTypeName}/Import({remoteFile.Id})",
        };
    }

    /// <summary>
    /// Get import form for object type (to get the file)
    /// </summary>
    [Authorize("managerplus")]
    [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Import({remoteFileId})/DataForm")]
    public async Task<Form> GetImportMapFormAsync([FromRoute] string objectTypeName, [FromRoute] Guid remoteFileId, [FromServices] RemoteFileService remoteFileService)
    {
        var remoteFile = await _connection.Filter<RemoteFile>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, remoteFileId)
            .Eq(x => x.EntityId, Context.UserId.Value)
            .FirstOrDefaultAsync();

        if (remoteFile == null) throw NotFoundException.New<RemoteFile>(remoteFileId);

        await using var stream = await remoteFileService.GetStreamAsync(Context, remoteFile);
        var parser = SpreadsheetFileParser.Create(remoteFile.ContentType, remoteFile.Name, stream);
        if (parser == null) throw new BadRequestException("Unsupported file type");

        var columns = parser.ColumnNames;

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        var form = await _objectTypeService.GetAddDataFormAsync(Context, objectType);

        // hack for "people" objects
        // when there are full name, first name and last name fields 
        // and the first and last are calculated from the full name
        // var fullNameField = form.Fields
        //     .OfType<TextField>()
        //     .FirstOrDefault(x => x.Label == "Full Name");
        // var fullNameHack = fullNameField!=null &&
        //                    objectType.Fields.Values.Any(x => x.Field.Label == "First Name" && x.InitialValue != null) &&
        //                    objectType.Fields.Values.Any(x => x.Field.Label == "Last Name" && x.InitialValue != null) &&
        //                    form.Fields.All(x => x.Label != "First Name" && x.Label != "Last Name" );

        var fields = form.Fields.Select(map);
        var layouts = form.Layouts;
        // if (fullNameHack)
        // {
        //     var items = getItems();
        //     fields = fields
        //         .Append(new SelectField
        //         {
        //             Name = "#firstName",
        //             Label = "First Name",
        //             SelectFieldOptions = new SelectFieldOptions
        //             {
        //                 Items = items,
        //             },
        //             // Visible = new[]
        //             // {
        //             //     $"{fullNameField.Name}=='{ImportObjectsJob.FullNameFormula}'"
        //             // },
        //             DefaultValue = items.Keys.FirstOrDefault(x => x.Contains("First Name", StringComparison.OrdinalIgnoreCase))
        //         })
        //         .Append(new SelectField
        //         {
        //             Name = "#lastName",
        //             Label = "Last Name",
        //             SelectFieldOptions = new SelectFieldOptions
        //             {
        //                 Items = items,
        //             },
        //             // Visible = new[]
        //             // {
        //             //     $"{fullNameField.Name}=='{ImportObjectsJob.FullNameFormula}'"
        //             // },
        //             DefaultValue = items.Keys.FirstOrDefault(x => x.Contains("Last Name", StringComparison.OrdinalIgnoreCase))
        //         });
        //
        //     if (layouts != null)
        //     {
        //         foreach (var layout in layouts.Values.OfType<GridFormLayout>())
        //         {
        //             for (var c = 0; c < layout.Rows.Length; c++)
        //             {
        //                 if (layout.Rows[c].Fields.Any(f => f.Name == fullNameField.Name))
        //                 {
        //                     var newRows = layout.Rows[..(c+1)]
        //                         .Append(new GridFormRowLayout
        //                         {
        //                             Fields = new[]
        //                             {
        //                                 new GridFormFieldLayout
        //                                 {
        //                                     Name = "#firstName",
        //                                     Width = 1
        //                                 },
        //                                 new GridFormFieldLayout
        //                                 {
        //                                     Name = "#lastName",
        //                                     Width = 1
        //                                 },
        //                             }
        //                         })
        //                         .Concat(layout.Rows[(c + 1)..])
        //                         .ToArray();
        //
        //                     layout.Rows = newRows;
        //                     break;
        //                 }
        //             }
        //         }
        //     }
        // }

        // TODO: look for matching config from previous import using hash of column names/object type?
        // ...

        return new Form
        {
            Name = $"{objectTypeName}_Import_Mapping",
            Title = "Import Mapping",
            Fields = fields.ToArray(),
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Finish",
                    Label = "Finish"
                }
            },
            Layouts = layouts,
        };

        string toCol(int c)
        {
            var h = c % 26;
            var l = c / 26;
            var cl = l == 0 ? "" : $"{(char)('A' + (l - 1))}";
            var ch = (char)('A' + h);
            return $"{cl}{ch}";
        }

        Dictionary<string, string> getItems()
        {
            var items = new Dictionary<string, string>(
                columns
                    .Select((x, i) => new KeyValuePair<string, string>(string.IsNullOrWhiteSpace(x) ? null : "{{SOURCE.[" + x + "]}}", $"{x} (Column {toCol(i)})"))
                    .Where(x => x.Key != null)
                    .DistinctBy(x => x.Key)
            );

            return items;
        }

        FormField map(FormField src)
        {
            if (src.IsReadOnly || !src.IsVisible) return src;

            return src switch
            {
                AddressField => mapField(),
                CheckboxField => mapField(),
                DateField => mapField(),
                DateTimeField => mapField(),
                EmailField => mapField(),
                GenericField => mapField(),
                NumberField => mapField(),
                PasswordField => mapField(),
                PhoneField => mapField(),
                PostalCodeField => mapField(),
                TextField => mapField(),
                TimeField => mapField(),
                UrlField => mapField(),

                ReferenceField x => mapReferenceField(x),
                SelectField x => mapSelectField(x),
                TagsField => src,

                ArrayField => src,
                ChildrenField => src,
                DateRangeField => src,
                DictionaryField => src,
                FileField => src,
                HiddenField => src,
                LabelField => src,
                MultiReferenceField => src,
                MultiSelectField => src,
                ObjectField => src,
                _ => src,
            };

            FormField mapSelectField(SelectField field)
            {
                // if (canBeCreatedOnDemand.Contains(referenceField.Name))
                // {
                var items = getItems();
                if (field.SelectFieldOptions.Items?.Keys != null)
                {
                    foreach (var key in field.SelectFieldOptions.Items.Keys)
                    {
                        var value = field.SelectFieldOptions.Items[key];
                        if (value == null) continue;
                        items.TryAdd(key.ToString(), value.ToString());
                    }
                }

                field.SelectFieldOptions.Items = items;
                // }

                return field;
            }

            FormField mapReferenceField(ReferenceField field)
            {
                // if (canBeCreatedOnDemand.Contains(referenceField.Name))
                // {
                var items = getItems();
                if (field.ReferenceFieldOptions.Items?.Keys != null)
                {
                    foreach (var key in field.ReferenceFieldOptions.Items.Keys)
                    {
                        var value = field.ReferenceFieldOptions.Items[key];
                        if (value == null) continue;
                        items.TryAdd(key.ToString(), value.ToString());
                    }
                }

                field.ReferenceFieldOptions.Items = items;
                // }

                return field;
            }

            FormField mapField()
            {
                var items = getItems();

                // if (src is TextField && fullNameHack && (src.Label ?? src.Name) == "Full Name")
                // {
                //     items.TryAdd(ImportObjectsJob.FullNameFormula, "=CONCATENATE([First Name], \" \", [Last Name])");
                // }

                return new SelectField
                {
                    Name = src.Name,
                    Label = src.Label,
                    IsRequired = src.IsRequired,
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = items
                    },
                    DefaultValue = items.Keys.FirstOrDefault(x => x.Contains(src.Label ?? src.Name, StringComparison.OrdinalIgnoreCase))
                };
            }
        }
    }

    /// <summary>
    /// import file
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Import({remoteFileId})/DataForm")]
    [Authorize("managerplus")]
    public async Task<DataFormActionResponse> ImportAsync([FromRoute] string objectTypeName, [FromRoute] Guid remoteFileId, [FromBody] DataFormActionRequest request)
    {
        var objectType = await _objectTypeService.GetAsync<ObjectTypeWithImportOptions>(Context, objectTypeName);
        if (objectType == null) throw NotFoundException.New(objectTypeName);
        if (objectType.ImportOptions == null || !objectType.RBAC.Can(Context, ObjectTypePermission.Import)) throw new ForbiddenException(Context, $"Import {objectTypeName}");

        var job = new ImportObjectsJob
        {
            AccountId = Context.AccountId.Value,
            EntityId = Context.UserId.Value,
            TargetObjectType = objectTypeName,
            Name = $"Import {objectTypeName}",
            // Description = $"Import {objectTypeName} started on {}",
            CreatedOn = DateTime.UtcNow,
            SourceRemoteFileId = remoteFileId,
            Mapping = request.Parameters,
            FlowId = objectType.ImportOptions.ImportJobFlowId,
            ObjectStatusId = objectType.ImportOptions.ImportJobObjectStatusId,
        };

        await _connection.InsertAsync(job);

        await _objectTypeService.FireCreateEventAsync(Context, job, e =>
        {
            e.Description ??= $"Queued import job for {objectTypeName}";
            e.Action ??= "ObjectCreated";
            e.AddRefValue(nameof(RemoteFile), remoteFileId);
        });

        return new DataFormActionResponse
        {
            Success = true,
            Message = "Import queued. You will be notified when it is complete",
        };
    }

    /// <summary>
    /// Get Form to generate and save a custom form for object type
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]({objectId})/BuildForm/DataForm")]
    public async Task<Form> GetBuildFormAsync([FromRoute] Guid objectId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectId);
        if (objectType == null) throw NotFoundException.New<ObjectType>(objectId);

        return new Form
        {
            Name = "BuildForm",
            Title = $"Generate Custom Form for {objectType.Name}",
            Fields = new FormField[]
            {
                new SelectField
                {
                    Name = nameof(FormName),
                    Label = "Type",
                    IsRequired = true,
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = new Dictionary<string, string>
                        {
                            { "Add", nameof(FormName.Add) },
                            { "Details", nameof(FormName.Details) },
                            { "Edit", nameof(FormName.Edit) },
                            { "View", nameof(FormName.View) },
                        }
                    }
                },
                new MultiReferenceField
                {
                    Name = nameof(AppForm.ProfileIds),
                    Label = "Profile Ids",
                    MultiReferenceFieldOptions = new MultiReferenceFieldOptions
                    {
                        ObjectType = nameof(AppProfile),
                    },
                    Visible = new[] { $"!{nameof(AppForm.Role)}" }
                },
                new SelectField
                {
                    Name = nameof(AppForm.Role),
                    Label = "Role",
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = new Dictionary<string, string>
                        {
                            { "Admin", nameof(EntityRoleId.Admin) },
                            { "Manager", nameof(EntityRoleId.Manager) },
                            { "User", nameof(EntityRoleId.User) },
                        }
                    },
                    Visible = new[] { $"!{nameof(AppForm.ProfileIds)}" }
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Add",
                    Label = "Generate",
                }
            }
        };
    }

    [Authorize("managerplus")]
    [HttpPost("/api/v1/[controller]({objectId})/BuildForm/DataForm")]
    public async Task<DataFormActionResponse> ExecBuildFormAsync([FromRoute] Guid objectId,
        [FromBody] DataFormActionRequest request)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectId);
        if (objectType == null) throw NotFoundException.New<PI.Shared.Models.ObjectType>(objectId);

        var entityRoleId = default(EntityRoleId?);
        if (request.Parameters.TryGetStrParam(nameof(AppForm.Role), out var role) && role != null)
        {
            if (!Enum.TryParse<EntityRoleId>(role, out var id))
            {
                return new DataFormActionResponse(request, "Invalid role");
            }

            entityRoleId = id;
        }

        var profileIds = default(Guid[]);
        if (request.Parameters.TryGetParam(nameof(AppForm.ProfileIds), out var profileObj) &&
            profileObj is JArray jArray)
        {
            profileIds = jArray.EnumerateChildren<string>()
                .Select(x => Guid.TryParse(x, out var uuid) ? uuid : throw new BadRequestException("Invalid Profile")
                )
                .ToArray();

            if (profileIds.Length < 1) profileIds = null;
        }

        if (!entityRoleId.HasValue && profileIds == null)
        {
            return new DataFormActionResponse(request, "Provide Profile(s) or Role");
        }

        if (entityRoleId.HasValue && profileIds != null)
        {
            return new DataFormActionResponse(request, "Provide Profile(s) or Role, not both");
        }

        if (!request.TryGetStrParam(nameof(FormName), out var formNameStr) ||
            !Enum.TryParse<FormName>(formNameStr, out var formName))
        {
            return new DataFormActionResponse(request, "Form Type is required");
        }

        await DisableMatchingAsync<AppForm>(objectType, objectType.GetFormName(formName), entityRoleId, profileIds);
        
        var context = Context.DeriveUserContext(entityRoleId, profileIds?.FirstOrDefault());
        var form = formName switch
        {
            FormName.Add => await _objectTypeService.BuildAddFormAsync(context, objectType, true),
            _ => await _objectTypeService.BuildEditFormAsync(context, objectType, formName),
        };

        var appForm = new AppForm
        {
            Id = Guid.NewGuid(),
            AccountId = Context.AccountId.Value,
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor,
            ObjectType = objectType.FullName,
            Form = form,
            Role = entityRoleId,
            ProfileIds = profileIds,
            IsActive = true,
            Name = objectType.GetFormName(formName),
        };

        await _connection.InsertAsync(appForm);

        return new DataFormActionResponse(request, "Form Created", true);
    }

    /// <summary>
    /// Disable previous versions of matching object type profile elements
    /// </summary>
    private async Task DisableMatchingAsync<T>(ObjectType objectType, string name, EntityRoleId? entityRoleId, Guid[] profileIds)
        where T: IObjectTypeProfileElement
    {
        var query = _connection.Filter<T>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Name, name)
                .Eq(x => x.ObjectType, objectType.FullName)
                .Ne(x => x.IsActive, false)
            ;

        if (entityRoleId.HasValue)
        {
            query.Eq(x => x.Role, entityRoleId);
        }
        else
        {
            query.AnyIn(x => x.ProfileIds, profileIds);
        }

        await query.Update
            .Set(x => x.IsActive, false)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, Context.Actor)
            .UpdateManyAsync();
    }

    /// <summary>
    /// Get Form to generate and save a custom page for object type
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]({objectId})/BuildPage/DataForm")]
    public async Task<Form> GetBuildPageForm([FromRoute] Guid objectId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectId);
        if (objectType == null) throw NotFoundException.New<ObjectType>(objectId);

        return new Form
        {
            Name = "BuildPage",
            Title = $"Generate Custom Page for {objectType.Name}",
            Fields =
            [
                new TextField
                {
                    Name = "Name",
                    IsRequired = true,
                    DefaultValue = objectType.FullName,
                },
                new MultiReferenceField
                {
                    Name = nameof(AppForm.ProfileIds),
                    Label = "Profile Ids",
                    MultiReferenceFieldOptions = new MultiReferenceFieldOptions
                    {
                        ObjectType = nameof(AppProfile),
                    },
                    Visible = [$"!{nameof(AppForm.Role)}"]
                },
                new SelectField
                {
                    Name = nameof(AppForm.Role),
                    Label = "Role",
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = new Dictionary<string, string>
                        {
                            { "Admin", nameof(EntityRoleId.Admin) },
                            { "Manager", nameof(EntityRoleId.Manager) },
                            { "User", nameof(EntityRoleId.User) },
                        }
                    },
                    Visible = [$"!{nameof(AppForm.ProfileIds)}"]
                }
            ],
            Actions =
            [
                new FormAction
                {
                    Name = "Add",
                    Label = "Generate",
                }
            ]
        };
    }

    [Authorize("managerplus")]
    [HttpPost("/api/v1/[controller]({objectId})/BuildPage/DataForm")]
    public async Task<DataFormActionResponse> ExecBuildPageAsync([FromRoute] Guid objectId,
        [FromBody] DataFormActionRequest request)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectId);
        if (objectType == null) throw NotFoundException.New<ObjectType>(objectId);

        var entityRoleId = default(EntityRoleId?);
        if (request.Parameters.TryGetStrParam(nameof(AppForm.Role), out var role) && role != null)
        {
            if (!Enum.TryParse<EntityRoleId>(role, out var id))
            {
                return new DataFormActionResponse(request, "Invalid role");
            }

            entityRoleId = id;
        }

        if (!request.Parameters.TryGetStrParam("Name", out var name)) name = "Default";

        var profileIds = default(Guid[]);
        if (request.Parameters.TryGetParam(nameof(AppForm.ProfileIds), out var profileObj) &&
            profileObj is JArray jArray)
        {
            profileIds = jArray.EnumerateChildren<string>()
                .Select(x => Guid.TryParse(x, out var uuid) ? uuid : throw new BadRequestException("Invalid Profile")
                )
                .ToArray();

            if (profileIds.Length < 1) profileIds = null;
        }

        if (!entityRoleId.HasValue && profileIds == null)
        {
            return new DataFormActionResponse(request, "Provide Profile(s) or Role");
        }

        if (entityRoleId.HasValue && profileIds != null)
        {
            return new DataFormActionResponse(request, "Provide Profile(s) or Role, not both");
        }
        
        await DisableMatchingAsync<AppPage>(objectType, name, entityRoleId, profileIds);

        var context = Context.DeriveUserContext(entityRoleId, profileIds?.FirstOrDefault());

        var page = await _objectTypeService.BuildLayoutPageAsync(context, objectType);
        
        var appForm = new AppPage
        {
            Id = Guid.NewGuid(),
            AccountId = Context.AccountId.Value,
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor,
            ObjectType = objectType.Name,
            Role = entityRoleId,
            ProfileIds = profileIds,
            IsActive = true,
            Name = name,
            Page = page,
        };

        await _connection.InsertAsync(appForm);

        return new DataFormActionResponse(request, "Page Created", true);
    }


    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]({objectTypeName})/Profile({profileId})/Form({formName})/DataForm")]
    public async Task<Form> GetFormForObjectTypeAsync(
        [FromRoute] string objectTypeName,
        [FromRoute] Guid profileId,
        [FromRoute] FormName formName,
        [FromQuery] EntityRoleId? roleId = null)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw NotFoundException.New($"{objectTypeName} not found");

        var profile = await _connection.Filter<AppProfile>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, profileId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (profile == null) throw NotFoundException.New<AppProfile>(profileId);

        if (profile.RoleId.HasValue) roleId = profile.RoleId;
        var clientId = profile.ClientId ?? Context.ClientId;

        IEntityContext context = roleId switch
        {
            EntityRoleId.Admin => UserContext.Admin(Context.UserId.Value, "[User]", Context.AccountId.Value, clientId, profileId),
            EntityRoleId.Manager or EntityRoleId.User => UserContext.OrgUser(Context.UserId.Value, "[User]", roleId.Value, Guid.Empty, Context.AccountId.Value, clientId, profileId),
            null or EntityRoleId.Profile => ProfileContext.Create(profileId, Context.AccountId.Value, Context.UserId.Value, clientId),
            _ => throw new BadRequestException("Invalid Role"),
        };

        var form = formName switch
        {
            FormName.Add => await _objectTypeService.GetAddDataFormAsync(context, objectType),
            _ => await _objectTypeService.GetEditDataFormAsync(context, objectType, Guid.Empty, new Dictionary<string, object>(), formName),
        };

        form.Title = $"{objectTypeName}({formName}) for {profile.Name}";
        form.Actions = null;

        form.Menu = new Menu
        {
            Name = "Form",
            Label = "Popup",
            Items =
            [
                new ActionMenuItem
                {
                    Icon = nameof(Icons.Design),
                    Name = FormAction.Client_Design,
                    Label = "Design",
                    Action = FormAction.Client_Design,
                }
            ]
        };

        return form;
    }

    /// <summary>
    /// Get "Save layouts" for form (id in query or as part of the route) 
    /// </summary>
    [Authorize("admin")]
    // [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Layout/Save/DataForm")]
    // [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/{formName}/Layout/Save/DataForm")]
    // [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[a-z_0-9\\.]]+$)})/Layout/Save/DataForm")]
    // [HttpGet("/api/v1/[controller]({objectTypeName:regex(^[[a-z_0-9\\.]]+$)})/{formName}/Layout/Save/DataForm")]
    // [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}({objectId})/Layout/Save/DataForm")]
    // [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}({objectId})/{formName}/Layout/Save/DataForm")]
    [HttpGet("/api/v1/[controller]({objectTypeName})/Profile({profileId})/Form({formName})/Layout/Save/DataForm")]
    public Form GetSaveLayoutForm(
        [FromRoute] string objectTypeName,
        [FromRoute] Guid profileId,
        [FromRoute] FormName formName)
    {
        return new Form
        {
            Name = "SaveLayouts",
            Title = "Save Layouts",
            Fields =
            [
                // new TextField
                // {
                //     Name = "Name",
                //     IsRequired = true,
                //     DefaultValue = $"{objectTypeName}: {formName}",
                // },
                // new TextField
                // {
                //     Name = "Description",
                //     IsRequired = false,
                //     TextFieldOptions = new TextFieldOptions
                //     {
                //         Multline = true,
                //     },
                //     DefaultValue = $"{formName} for {objectTypeName}",
                // },
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
                    Visible = [$"!{nameof(AppProfileElement.Role)}"],
                    DefaultValue = new[] { profileId },
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
                    Visible = [$"!{nameof(AppProfileElement.ProfileIds)}"],
                }
            ],
            Actions =
            [
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
                }
            ]
        };
    }

    [Authorize("admin")]
    // [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Layout/Save")]
    // [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/{formName}/Layout/Save")]
    // [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[a-z_0-9\\.]]+$)})/Layout/Save")]
    // [HttpPost("/api/v1/[controller]({objectTypeName:regex(^[[a-z_0-9\\.]]+$)})/{formName}/DataForm/Layout/Save")]
    // [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}({objectId})/Layout/Save")]
    // [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}({objectId})/{formName}/Layout/Save")]
    [HttpPost("/api/v1/[controller]({objectTypeName})/Profile({profileId})/Form({formName})/Layout/Save")]
    public async Task<BreakpointLayouts> SaveLayoutAsync(
        [FromRoute] string objectTypeName,
        [FromRoute] Guid profileId,
        [FromRoute] FormName formName,
        [FromBody] SaveFormLayoutsRequest request
    )
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
            if (!objectType.GetLoadedBaseObjectTypeNames().Contains(objectTypeName)) throw new BadRequestException($"{objectType.Name} does not extend {objectTypeName}");
            objectTypeName = objectType.Name;
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

        // TODO: fire event 
        // ...

        return layout.Layouts;
    }

    // /// <summary>
    // /// is it used anywhere ?
    // /// should return a "flat object" instead
    // /// </summary>
    // [Obsolete("should use CustomController")]
    // [Authorize("admin")]
    // [HttpGet("{objectType}({id})")]
    // public async Task<object> GetObjectTypeByIdAsync([FromRoute] Guid id, [FromRoute] string objectType)
    // {
    //     var o = await _objectTypeService.GetFlowObjectAsync(Context, objectType, id);
    //     if (o == null) throw new NotFoundException(objectType, id);
    //     return o;
    // }
    //
    // /// <summary>
    // /// need here until there is a listener that will automatically initialize the object once one is created
    // /// </summary>
    // [Obsolete]
    // [Authorize("default")]
    // [HttpGet("DataForm")]
    // public async Task<Form> GetEditFormAsync([FromQuery] Guid? id)
    // {
    //     var form = await _objectTypeService.GetDataFormAsync(Context, nameof(PI.Shared.Models.ObjectType), id);
    //     if (form == null) throw new NotFoundException(nameof(ObjectType), id);
    //     return form;
    // }
    //
    // /// <summary>
    // /// need here until there is a listener that will automatically initialize the object once one is created
    // /// </summary>
    // [Obsolete]
    // [Authorize("default")]
    // [HttpPost("DataForm")]
    // public async Task<DataFormActionResponse> EditFormOnActionAsync([FromBody] DataFormActionRequest request)
    // {
    //     var result = await _objectTypeService.ExecObjectTypeActionAsync(Context, request);
    //     return result;
    // }
    //
    // // [DisableFormValueModelBinding]
    // [Obsolete]
    // [Authorize("managerplus")]
    // [HttpPost("/api/v1/[controller]({id})/Import")]
    // [Consumes("application/octet-stream", "multipart/form-data")]
    // public async Task<IActionResult> ImportCsvAsync([FromRoute] Guid id, [FromForm] IFormFile file)
    // {
    //     var objectType = await _objectTypeService.GetAsync(Context, id);
    //     if (objectType == null) throw NotFoundException.New<PI.Shared.Models.ObjectType>(id);
    //
    //     var result = await _objectTypeService.ImportCsvAsync(Context, objectType, file.OpenReadStream());
    //     return Ok(result);
    // }
}