using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace Controllers;

[Route("/api/v1/[controller]")]
[Authorize("default")]
public class AppConfigController : APIController
{
    private readonly MongoConnection _connection;

    public AppConfigController(MongoConnection connection)
    {
        _connection = connection;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AppConfig), 200)]
    public async Task<AppConfig> GetAppConfigAsync()
    {
        var result = await GetProfileAsync();
        if (result == null) throw new ForbiddenException(Context, "No profile");

        return BuildAppConfig(result);
    }

    // [Authorize("admin")]
    // [HttpGet("/api/v1/AppConfig({id})")]
    // [ProducesResponseType(typeof(AppConfig), 200)]
    // public async Task<IActionResult> GetConfigAsync([FromRoute] Guid id)
    // {
    //     var result = await _connection.Filter<AppProfile>()
    //         .Eq(x => x.Id, id)
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .FirstOrDefaultAsync();

    //     return result == null ? (IActionResult)NotFound() : Content(result.CompiledJson, "application/json");
    // }

    [Authorize("admin")]
    [HttpPost]
    public async Task<AppConfig> CreateAsync(string name, string description, string roleStr)
    {
        if (!Enum.TryParse<EntityRoleId>(roleStr, out var role))
        {
            throw new BadRequestException($"Invalid Role: {roleStr}");
        }

        var profile = new AppProfile
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            AccountId = Context.AccountId.Value,
            Name = name,
            Description = description,
        };

        await _connection.InsertAsync(profile);
        return BuildAppConfig(profile);
    }

    // [HttpPost("/api/v1/AppConfig/Compile")]
    // [ProducesResponseType(typeof(AppConfig), 200)]
    // public async Task<IActionResult> CompileAllAsync()
    // {
    //     var profiles = await _connection.Filter<AppProfile>()
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .FindAsync();

    //     foreach (var profile in profiles)
    //     {
    //         await CompileProfileAsync(profile);
    //     }

    //     return Ok();
    // }

    private async Task<T> GetAsync<T>(string name)
        where T : AppProfileElement
    {
        var ele = await _connection.GetProfileElementAsync<T>(Context, name);
        if (ele == null) throw new NotFoundException($"{name} not found");
        return ele;
    }

    [HttpGet("Page/{name}")]
    [HttpGet("{name}/DataPage")]
    [ProducesResponseType(typeof(Page), 200)]
    public async Task<IActionResult> GetPageFromConfigAsync([FromRoute] string name)
    {
        var page = (await GetAsync<AppPage>(name)).Page;
        
        return Ok(page);
    }

    [HttpGet("DataView/{name}")]
    public async Task<DataView> GetDataViewFromConfigAsync([FromRoute] string name)
        => (await GetAsync<AppDataView>(name)).DataView;

    [HttpGet("Form/{name}")]
    public async Task<Form> GetFormFromConfigAsync([FromRoute] string name)
        => (await GetAsync<AppForm>(name)).Form;

    [HttpGet("Menu/{name}")]
    public async Task<Menu> GetMenuFromConfigAsync([FromRoute] string name)
    {
        var menu = (await GetAsync<AppMenu>(name)).Menu;

        menu.FillPlaceHolders(Context);

        return menu;
    }

    [HttpGet("Menu")]
    public async Task<Menu> GetManiMenuFromConfigAsync()
    {
        var profile = await GetProfileAsync();
        if (string.IsNullOrWhiteSpace(profile?.InitialMenu))
        {
            throw new NotFoundException("Can't determine default menu");
        }

        var menu = (await _connection.Filter<AppMenu>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Name, profile.InitialMenu)
            .FirstOrDefaultAsync()).Menu;

        menu.FillPlaceHolders(Context);

        return menu;
    }

    // [Authorize("admin")]
    // [HttpGet("/api/v1/Page/{pageName}")]
    // [ProducesResponseType(typeof(AppPage), 200)]
    // public async Task<IActionResult> GetPageAsync([FromRoute] string pageName)
    // {
    //     var page = await _connection.Filter<AppPage>()
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .Eq(x => x.Name, pageName)
    //         .FirstOrDefaultAsync();

    //     if (page == null) return NotFound("Page");

    //     return Ok(page);
    // }

    // [Authorize("admin")]
    // [HttpPost("/api/v1/Page/{pageName}")]
    // public async Task<AppPage> CompileAsync([FromRoute] string pageName)
    // {
    //     var page = await _connection.Filter<AppPage>()
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .Eq(x => x.Name, pageName)
    //         .FirstOrDefaultAsync();

    //     if (page != null) throw new BadRequestException($"There is already a page named '{pageName}'");

    //     page = new AppPage
    //     {
    //         Id = Guid.NewGuid(),
    //         AccountId = PI.Shared.Constants.AccountIds.FCI,
    //         Name = pageName,
    //         Page = new GridPage
    //         {
    //             Name = pageName,
    //             Grid = $"/api/v1/CustomObject/{pageName}"
    //         }
    //     };

    //     await _connection.InsertAsync(page);

    //     var profile = await _connection.Filter<AppProfile>()
    //         .Eq(x => x.Id, Context.ProfileId.Value)
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .Update
    //             .AddToSet(x => x.Pages, page.Name)
    //         .UpdateAndGetOneAsync();

    //     return page;
    // }

    /// <summary>
    /// Get Forms (DataView)
    /// TODO: use AppDataView instead?
    /// </summary>
    [Obsolete]
    [Authorize("admin")]
    [HttpPost("Form/DataView")]
    [ProducesResponseType(typeof(DataViewResponse), 200)]
    public async Task<IActionResult> GetFormsDataViewAsync([FromBody] DataViewRequest request)
    {
        var response = new DataViewResponse
        {
            Request = request,
            View = new DataView
            {
                Name = "Forms",
                Fields = new FormField[]
                {
                    new HiddenField
                    {
                        Name = "id",
                        Label = "Id"
                    },
                    new TextField
                    {
                        Name = "name",
                        Label = "Name"
                    },
                    new TextField
                    {
                        Name = "description",
                        Label = "Description"
                    },
                    new DateTimeField
                    {
                        Name = "createdOn",
                        Label = "Created On"
                    }
                },
                Detail = new DataViewDetail
                {
                    Page = "Form"
                }
            },
            Result = await _connection.Filter<AppForm>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .ExcludeField(x => x.Form)
                .FindAsync()
        };

        return Ok(response.UpdateFields());
    }

    // [Authorize("admin")]
    // [HttpPost("Form")]
    // [StringBody("application/json")]
    // public async Task<AppForm> AddFormAsync()
    // {
    //     var json = Request.GetBody()?.Trim();
    //     var form = JsonConvert.DeserializeObject<Form>(json);
    //     if (string.IsNullOrEmpty(form?.Name)) throw new BadRequestException("Missing Name");
    //     if (form.Fields == null || form.Fields.Length < 1) throw new BadRequestException("Missing Fields");

    //     var existing = await _connection.Filter<AppForm>()
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .Eq(x => x.Name, form.Name)
    //         .FirstOrDefaultAsync();

    //     if (existing != null) throw new BadRequestException("Form with same name already exists");

    //     existing = new AppForm
    //     {
    //         AccountId = Context.AccountId.Value,
    //         Name = form.Name,
    //         Form = form,
    //         LastActor = Context.Actor
    //     };

    //     await _connection.InsertAsync(existing);

    //     await _connection.Filter<AppProfile>()
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .Eq(x => x.Id, Context.ProfileId.Value)
    //         .Update
    //             .AddToSet(x => x.Forms, existing.Name)
    //         .UpdateAndGetOneAsync();

    //     return existing;
    // }

    // [HttpPost("/api/v1/[controller]({id})/Submenus")]
    // [ProducesResponseType(typeof(AppConfig), 200)]
    // public async Task<IActionResult> ExtractSubmenusAsync([FromRoute] Guid id)
    // {
    //     var profile = await _connection.Filter<AppProfile>()
    //         .Eq(x => x.Id, id)
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .FirstOrDefaultAsync();

    //     if (profile == null) return NotFound();

    //     profile = await ExtractSubMenusAsync(profile);
    //     var config = await BuildAppConfig(profile);
    //     return Ok(Compile(config));
    // }

    [Authorize("admin")]
    [HttpPost("Page")]
    [ProducesResponseType(typeof(AppPage), 200)]
    public async Task<IActionResult> AddPageAsync([FromBody] Page body)
    {
        var row = new AppPage
        {
            Id = Guid.NewGuid(),
            AccountId = AccountIds.FCI,
            Name = body.Name,
            Page = body
        };

        await _connection.InsertAsync(row);

        return Ok(row);
    }

    [Authorize("admin")]
    [HttpPost("DataView")]
    [ProducesResponseType(typeof(AppDataView), 200)]
    public async Task<IActionResult> AddDataViewAsync([FromBody] DataView body)
    {
        var row = new AppDataView
        {
            Id = Guid.NewGuid(),
            AccountId = AccountIds.FCI,
            Name = body.Name,
            DataView = body
        };

        await _connection.InsertAsync(row);

        return Ok(row);
    }

    // [Authorize("admin")]
    // [HttpPost("Import")]
    // [ProducesResponseType(typeof(AppConfig), 200)]
    // [StringBody]
    // public async Task<IActionResult> ImportConfigAsync()
    // {
    //     var json = Request.GetBody()?.Trim();
    //     var config = JsonConvert.DeserializeObject<AppConfig>(json);

    //     if (config.Menus != null)
    //     {
    //         var menus = config.Menus.Select(x => new AppMenu
    //         {
    //             Id = Guid.NewGuid(),
    //             AccountId = PI.Shared.Constants.AccountIds.FCI,
    //             Name = x.Name,
    //             Menu = x
    //         });

    //         foreach (var menu in menus)
    //         {
    //             await _connection.InsertAsync(menu);
    //         }
    //     }

    //     if (config.Forms != null)
    //     {
    //         var forms = config.Forms.Select(x => new AppForm
    //         {
    //             Id = Guid.NewGuid(),
    //             AccountId = PI.Shared.Constants.AccountIds.FCI,
    //             Name = x.Name,
    //             Form = x
    //         });

    //         foreach (var form in forms)
    //         {
    //             await _connection.InsertAsync(form);
    //         }
    //     }

    //     if (config.Pages != null)
    //     {
    //         var pages = config.Pages.Select(x => new AppPage
    //         {
    //             Id = Guid.NewGuid(),
    //             AccountId = PI.Shared.Constants.AccountIds.FCI,
    //             Name = x.Name,
    //             Page = x
    //         });

    //         foreach (var page in pages)
    //         {
    //             await _connection.InsertAsync(page);
    //         }
    //     }

    //     if (config.Datagrids != null)
    //     {
    //         var dataviews = config.Datagrids.Select(x => new AppDataView
    //         {
    //             Id = Guid.NewGuid(),
    //             AccountId = PI.Shared.Constants.AccountIds.FCI,
    //             Name = x.Name,
    //             DataView = x
    //         });

    //         foreach (var dataview in dataviews)
    //         {
    //             await _connection.InsertAsync(dataview);
    //         }
    //     }

    //     return Ok(config);
    // }

    // [HttpPost("/api/v1/[controller]({id})/Compile")]
    // [ProducesResponseType(typeof(AppConfig), 200)]
    // public async Task<IActionResult> CompileAsync([FromRoute] Guid id)
    // {
    //     var profile = await _connection.Filter<AppProfile>()
    //         .Eq(x => x.Id, id)
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .FirstOrDefaultAsync();

    //     if (profile == null) return NotFound();

    //     return await ReCompileAsync(profile);
    // }

    // /// <summary>
    // /// Get App Config based on appt type
    // /// TODO: change route/move action
    // /// </summary>
    // [HttpGet("/api/v1/AppointmentType({id})/[controller]")]
    // [ProducesResponseType(typeof(AppConfig), 200)]
    // public async Task<IActionResult> GetAppointmentTypeAppConfigAsync([FromRoute] Guid id)
    // {
    //     // TODO: enforce rights to access appointment type
    //     // ...

    //     var result = await _appointmentTypeAdapter.GetConfigAsync(id);
    //     if (result == null) return NotFound();

    //     return Content(result, "application/json");
    // }

    /// <summary>
    /// Set app configuration for an appt type
    /// TODO: change route/move action 
    /// </summary>
    [Authorize("admin")]
    [HttpPost("/api/v1/AppointmentType({id})/[controller]")]
    [ProducesResponseType(typeof(AppConfig), 200)]
    public Task<IActionResult> SetAppConfigAsync([FromRoute] Guid id, [FromBody] dynamic body)
    {
        // // TODO: enforce rights to access appointment type
        // // ...

        // // for now only makes sure it is valid json
        // // doens't validate the schema
        // string result = JsonConvert.SerializeObject(body);

        // // var obj = JsonConvert.DeserializeObject<AppConfig>(result);
        // await _appointmentTypeAdapter.SetConfigAsync(id, result);
        // // if (result == null) return NotFound();

        // return Content(result, "application/json");
        throw new NotImplementedException();
    }

    // private async Task<IActionResult> ReCompileAsync(AppProfile profile)
    // {
    //     var result = await CompileProfileAsync(profile);

    //     return Content(result, "application/json");
    // }

    // private async Task<string> CompileProfileAsync(AppProfile profile)
    // {
    //     var config = BuildAppConfig(profile);
    //     return Compile(config);
    // }

    // private async Task<AppProfile> ExtractSubMenusAsync(AppProfile profile)
    // {
    //     var menus = (await _connection.Filter<AppMenu>()
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .In(x => x.Name, profile.Menus)
    //         .FindAsync());

    //     var count = menus.Count;
    //     for (var c = 0; c < menus.Count; c++)
    //     {
    //         var menu = menus[c];
    //         ExtractSubMenu(menu.Name, menu, menus);
    //     }

    //     if (menus.Count > count)
    //     {
    //         // save menus
    //         for (var c = count; c < menus.Count; c++)
    //         {
    //             await _connection.InsertAsync(menus[c]);
    //         }

    //         profile.Id = Guid.NewGuid();
    //         profile.CreatedOn = DateTime.UtcNow;
    //         profile.Menus = menus.Select(x => x.Name).ToArray();

    //         await _connection.InsertAsync(profile);
    //     }

    //     return profile;
    // }

    // private void ExtractSubMenu(string path, AppMenu menu, List<AppMenu> subMenus)
    // {
    //     foreach (var sub in menu.Menu.Items.OfType<Menu>().Where(x => x.Items != null))
    //     {
    //         var subMenu = new AppMenu
    //         {
    //             Id = Guid.NewGuid(),
    //             CreatedOn = DateTime.UtcNow,
    //             AccountId = menu.AccountId,
    //             Menu = Clone(sub),
    //             Name = $"{path}.{sub.Name}"
    //         };

    //         subMenus.Add(subMenu);

    //         // clear 
    //         sub.Name = subMenu.Name;
    //         sub.Items = null;
    //         sub.Enable = null;
    //         sub.Label = null;
    //         sub.Visible = null;
    //     }
    // }

    // private Menu Clone(Menu sub)
    // {
    //     return JsonConvert.DeserializeObject<Menu>(JsonConvert.SerializeObject(sub));
    // }

    // private string Compile(AppConfig config)
    // {
    //     var json = JsonConvert.SerializeObject(config, new JsonSerializerSettings
    //     {
    //         DefaultValueHandling = DefaultValueHandling.Ignore,
    //         NullValueHandling = NullValueHandling.Ignore,
    //         Formatting = Formatting.None,
    //         ContractResolver = new DefaultContractResolver
    //         {
    //             NamingStrategy = new CamelCaseNamingStrategy()
    //         }
    //     });

    //     return json;
    // }

    private AppConfig BuildAppConfig(AppProfile profile)
    {
        var config = new AppConfig
        {
            Name = profile.Name,
            InitialPage = profile.InitialPage,
            Menu = profile.InitialMenu,
            // DisableHistory
        };

        return config;
    }

    private async Task<AppProfile> GetProfileAsync()
    {
        var profileId = Context.ProfileId.Value;
        var result = await _connection.Filter<AppProfile>()
            .Eq(x => x.Id, profileId)
            .FirstOrDefaultAsync();

        return result;
    }
}