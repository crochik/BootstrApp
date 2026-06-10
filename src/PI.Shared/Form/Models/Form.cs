using System;
using System.Collections.Generic;
using System.Linq;
using Crochik.Mongo;
using JsonSubTypes;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Models;
using PI.Shared.Models.Layout;
using Swashbuckle.AspNetCore.Annotations;

namespace PI.Shared.Form.Models;

/// <summary>
/// Field types used in LeadType and to import CSV
/// more than a "javascript type" but less than a "form type" - very conflicted idea
/// TODO: split it/refactor further to be one thing or the other
/// </summary>
[JsonConverter(typeof(StringEnumConverter), true)]
[Obsolete("probably better to replace it with a reference to a field")]
public enum FIELDTYPE
{
    Undefined,
    Address,
    Boolean,
    Date,
    Datetime,
    Email,
    Hidden,
    Number,
    Phone,
    Postalcode,
    Text,
    Time
}

public class FormAction : UIElement
{
    public const string Add = nameof(Add);
    public const string Update = nameof(Update);
    public const string Delete = nameof(Delete);
    public const string Edit = nameof(Edit);

    /// <summary>
    /// Save records in the client to csv
    /// </summary>
    public const string Client_ExportCurrentView = "#export";

    public const string Client_Design = "#design";
    public const string Client_Layout = "#layout";

    /// <summary>
    /// Generate csv on the server matching the dataview
    /// </summary>
    public const string Client_DonwloadCsv = "#csv";

    /// <summary>
    /// bulk import
    /// </summary>
    public const string Client_Upload = "#upload";

    /// <summary>
    /// Tell client to reload view
    /// </summary>
    public const string Client_Reload = "#reload";

    /// <summary>
    /// Tell client to create new object of type (for ObjectFields).
    /// </summary>
    public const string Client_New = "#new";

    /// <summary>
    /// Explicit reset of value
    /// </summary>
    public const string Client_Reset = "#reset";

    /// <summary>
    /// save view/form/...
    /// </summary>
    public const string Client_Save = "#save";

    /// <summary>
    /// Cancel form
    /// </summary>
    public const string Client_Cancel = "#cancel";

    /// <summary>
    /// Cancel form
    /// </summary>
    public const string Client_LoadView = "#view";

    public string Action { get; set; }
}

public enum FormName
{
    Add,
    Edit,
    View,
    Details,
};

[DiscriminatorWithFallback]
[BsonDiscriminator(Required = true)]
[JsonConverter(typeof(JsonSubtypes), "type")]
[JsonSubtypes.KnownSubType(typeof(GridFormLayout), "Grid")]
[BsonKnownTypes(typeof(GridFormLayout))]
[SwaggerDiscriminator("_t")]
[SwaggerSubType(typeof(GridFormLayout), DiscriminatorValue = nameof(GridFormLayout))]
public class FormLayout
{
    [JsonProperty("_t")] 
    [BsonIgnore] 
    // ReSharper disable once InconsistentNaming
    public string _t => GetType().Name;
    
    [JsonProperty("type")] [BsonIgnore] public virtual string Type => null;
    
    public ScreenBreakpoint Breakpoint { get; set; }
}

public class GridFormFieldLayout
{
    /// <summary>
    /// Field Name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 1-12
    /// </summary>
    public int Width { get; set; }
}

public class GridFormRowLayout
{
    public GridFormFieldLayout[] Fields { get; set; }

    public static GridFormRowLayout New(IEnumerable<string> cols) => new()
    {
        Fields = cols
            .Select(x => new GridFormFieldLayout
            {
                Name = x,
                Width = 1,
            })
            .ToArray(),
    };
}

public class GridFormLayout : FormLayout
{
    public override string Type => "Grid";

    public GridFormRowLayout[] Rows { get; set; }

    public static GridFormLayout New(ScreenBreakpoint breakpoint, IEnumerable<IEnumerable<string>> rows) => new()
    {
        Breakpoint = breakpoint,
        Rows = rows
            .Select(GridFormRowLayout.New)
            .ToArray(),
    };
}

// IForm
public class Form
{
    public const string RequiredFieldsName = "#requiredFields";

    [JsonProperty("type")] [BsonIgnore] public virtual string Type => GetType().Name;

    public string Name { get; set; }
    public string Title { get; set; }
    public FormField[] Fields { get; set; }
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Actions (bottom menu)
    /// </summary>
    [JsonProperty("actions")]
    public FormAction[] Actions { get; set; }

    /// <summary>
    /// top/context menu
    /// </summary>
    public Menu Menu { get; set; }

    /// <summary>
    /// Layouts for different breakpoints
    /// </summary>
    public BreakpointLayouts Layouts { get; set; }

    /// <summary>
    /// Object Type (optional) 
    /// </summary>
    public string ObjectType { get; set; }

    public static Form BuildErrorForm(string message, string title = null) => new()
    {
        Name = "Error",
        Title = title,
        Fields = new FormField[]
        {
            new LabelField
            {
                Name = "Message",
                Label = message
            }
        },
        Actions = new[]
        {
            new FormAction
            {
                Name = FormAction.Client_Cancel,
                Label = "OK"
            }
        }
    };
}