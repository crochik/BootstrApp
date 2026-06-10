using System.Collections.Generic;
using Crochik.Mongo;
using JsonSubTypes;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;
using PI.Shared.Models.Layout;
using Swashbuckle.AspNetCore.Annotations;

namespace PI.Shared.Form.Models;

[DiscriminatorWithFallback]
[BsonDiscriminator(Required = true)]
[BsonKnownTypes(
    typeof(FormPage),
    typeof(GridPage),
    typeof(ExternalPage),
    typeof(CustomPage),
    typeof(LayoutPage)
)]
[JsonConverter(typeof(JsonSubtypes))]
[JsonSubtypes.KnownSubTypeWithProperty(typeof(FormPage), "Form")]
[JsonSubtypes.KnownSubTypeWithProperty(typeof(GridPage), "Grid")]
[JsonSubtypes.KnownSubTypeWithProperty(typeof(ExternalPage), "External")]
[JsonSubtypes.KnownSubTypeWithProperty(typeof(CustomPage), "Custom")]
[JsonSubtypes.KnownSubTypeWithProperty(typeof(LayoutPage), "Layout")]
[SwaggerDiscriminator("_t")]
[SwaggerSubType(typeof(FormPage), DiscriminatorValue = nameof(FormPage))]
[SwaggerSubType(typeof(GridPage), DiscriminatorValue = nameof(GridPage))]
[SwaggerSubType(typeof(ExternalPage), DiscriminatorValue = nameof(ExternalPage))]
[SwaggerSubType(typeof(CustomPage), DiscriminatorValue = nameof(CustomPage))]
[SwaggerSubType(typeof(LayoutPage), DiscriminatorValue = nameof(LayoutPage))]
public class Page : UIElement
{
    [JsonProperty("_t")]
    // ReSharper disable once InconsistentNaming
    public virtual string _t => GetType().Name;

    /// <summary>
    /// Page Menu (to show in the overflow menu)
    /// </summary>
    public Menu Menu { get; set; }

    /// <summary>
    /// Name/Url for the menu to be loaded
    /// </summary>
    public string AppMenu { get; set; }

    /// <summary>
    /// Whether the app bar should be hidden
    /// </summary>
    public bool? HideAppBar { get; set; }
}

public class FormPage : Page
{
    public string Form { get; set; }
}

public class GridPage : Page
{
    public string Grid { get; set; }
}

public class ExternalPage : Page
{
    public string Url { get; set; }
    public bool OpenInNewWindow { get; set; }
}

[SwaggerSubType(typeof(LayoutPage), DiscriminatorValue = nameof(LayoutPage))]
public class CustomPage : Page
{
    public string ComponentName { get; set; }
}

public class LayoutPage : CustomPage
{
    public LayoutItem Layout { get; set; }

    public bool FillPlaceholders(IEntityContext context, IDictionary<string, object> objectContext)
        => Layout?.FillPlaceholders(context, objectContext) ?? true;
}