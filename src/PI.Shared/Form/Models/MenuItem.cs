using System.Collections.Generic;
using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Swashbuckle.AspNetCore.Annotations;

namespace PI.Shared.Form.Models;

public enum Icons
{
    Account,
    Action,
    Add,
    Agenda,
    Calendar,
    CalendarEvent,
    Call,
    Campaign,
    Cancel,
    Card,
    ComposeSMS,
    Contact,
    Delete,
    Design,
    Download,
    Edit,
    Email,
    Expand,
    Grid,
    Map,
    Menu,
    Money,
    More,
    MyLocation,
    Notifications,
    Place,
    Print,
    Refresh,
    Reminder,
    Remove,
    SMS,
    Save,
    Search,
    Settings,
    Tag, 
    Task, 
    Upload,
    View
}

[SwaggerDiscriminator("_t")]
[SwaggerSubType(typeof(ActionMenuItem), DiscriminatorValue=nameof(ActionMenuItem))]
[SwaggerSubType(typeof(PageMenuItem), DiscriminatorValue=nameof(PageMenuItem))]
[SwaggerSubType(typeof(Menu), DiscriminatorValue=nameof(Menu))]
[JsonConverter(typeof(JsonSubtypes), "_t")]
public class MenuItem : UIElement
{
    [JsonProperty("_t")]
    // ReSharper disable once InconsistentNaming
    public virtual string _t => GetType().Name;

    public string Icon { get; set; }

    public virtual void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext = null)
    {
        // replace place holders.... 
    }
}

public class ActionMenuItem : MenuItem
{
    public string Action { get; set; }
        
    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext = null)
    {
        if (ExpressionEvaluatorService.TryResolve(context, objectContext, Action, out var value) && value is string str)
        {
            Action = str;
        }
    }
}

public class PageMenuItem : MenuItem
{
    public string Page { get; set; }
        
    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext = null)
    {
        if (ExpressionEvaluatorService.TryResolve(context, objectContext, Page, out var value) && value is string str)
        {
            Page = str;
        }
    }
}

public class Menu : MenuItem
{
    public MenuItem[] Items { get; set; }
    public bool Collapsible { get; set; }
        
    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext = null)
    {
        if (Items?.Length > 0)
        {
            foreach (var item in Items)
            {
                item.FillPlaceHolders(context, objectContext);
            }
        }
    }
}