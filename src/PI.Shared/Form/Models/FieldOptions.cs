using System.Linq;
using System.Collections;
using System.Collections.Specialized;
using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using PI.Shared.Models.Expressions;
using System;
using MongoDB.Bson.Serialization.Attributes;
using Swashbuckle.AspNetCore.Annotations;

namespace PI.Shared.Form.Models;

[SwaggerDiscriminator("_t")]
[SwaggerSubType(typeof(AddressFieldOptions), DiscriminatorValue=nameof(AddressFieldOptions))]
[SwaggerSubType(typeof(AppointmentFieldOptions), DiscriminatorValue=nameof(AppointmentFieldOptions))]
[SwaggerSubType(typeof(ArrayFieldOptions), DiscriminatorValue=nameof(ArrayFieldOptions))]
[SwaggerSubType(typeof(BitwiseFlagFieldOptions), DiscriminatorValue = nameof(BitwiseFlagFieldOptions))]    
[SwaggerSubType(typeof(ChatReferenceFieldOptions), DiscriminatorValue=nameof(ChatReferenceFieldOptions))]
[SwaggerSubType(typeof(CheckboxFieldOptions), DiscriminatorValue=nameof(CheckboxFieldOptions))]
[SwaggerSubType(typeof(ChildrenFieldOptions), DiscriminatorValue=nameof(ChildrenFieldOptions))]
[SwaggerSubType(typeof(DateRangeFieldOptions), DiscriminatorValue=nameof(DateRangeFieldOptions))]
[SwaggerSubType(typeof(DictionaryFieldOptions), DiscriminatorValue=nameof(DictionaryFieldOptions))]
[SwaggerSubType(typeof(ExpressionFieldOptions), DiscriminatorValue=nameof(ExpressionFieldOptions))]
[SwaggerSubType(typeof(EmailFieldOptions), DiscriminatorValue=nameof(EmailFieldOptions))]
[SwaggerSubType(typeof(FileFieldOptions), DiscriminatorValue=nameof(FileFieldOptions))]
[SwaggerSubType(typeof(GenericFieldOptions), DiscriminatorValue=nameof(GenericFieldOptions))]
[SwaggerSubType(typeof(HiddenFieldOptions), DiscriminatorValue=nameof(HiddenFieldOptions))]
[SwaggerSubType(typeof(ImageFieldOptions), DiscriminatorValue=nameof(ImageFieldOptions))]
[SwaggerSubType(typeof(LabelFieldOptions), DiscriminatorValue=nameof(LabelFieldOptions))]
[SwaggerSubType(typeof(LocationDistanceFieldOptions), DiscriminatorValue=nameof(LocationDistanceFieldOptions))]
[SwaggerSubType(typeof(LocationFieldOptions), DiscriminatorValue=nameof(LocationFieldOptions))]
[SwaggerSubType(typeof(LookupFieldOptions), DiscriminatorValue=nameof(LookupFieldOptions))]
[SwaggerSubType(typeof(MultiReferenceFieldOptions), DiscriminatorValue=nameof(MultiReferenceFieldOptions))]
[SwaggerSubType(typeof(MultiSelectFieldOptions), DiscriminatorValue=nameof(MultiSelectFieldOptions))]
[SwaggerSubType(typeof(NumberFieldOptions), DiscriminatorValue=nameof(NumberFieldOptions))]
[SwaggerSubType(typeof(ObjectFieldOptions), DiscriminatorValue=nameof(ObjectFieldOptions))]
[SwaggerSubType(typeof(PasswordFieldOptions), DiscriminatorValue=nameof(PasswordFieldOptions))]
[SwaggerSubType(typeof(PhoneFieldOptions), DiscriminatorValue=nameof(PhoneFieldOptions))]
[SwaggerSubType(typeof(PostalCodeFieldOptions), DiscriminatorValue=nameof(PostalCodeFieldOptions))]
[SwaggerSubType(typeof(ReferenceFieldOptions), DiscriminatorValue=nameof(ReferenceFieldOptions))]
[SwaggerSubType(typeof(RelatedObjectsFieldOptions), DiscriminatorValue=nameof(RelatedObjectsFieldOptions))]
[SwaggerSubType(typeof(RemoteFileFieldOptions), DiscriminatorValue=nameof(RemoteFileFieldOptions))]
[SwaggerSubType(typeof(SelectFieldOptions), DiscriminatorValue=nameof(SelectFieldOptions))]
[SwaggerSubType(typeof(TagsFieldOptions), DiscriminatorValue=nameof(TagsFieldOptions))]
[SwaggerSubType(typeof(TextFieldOptions), DiscriminatorValue=nameof(TextFieldOptions))]
[SwaggerSubType(typeof(TimeFieldOptions), DiscriminatorValue=nameof(TimeFieldOptions))]
[SwaggerSubType(typeof(URLFieldOptions), DiscriminatorValue=nameof(URLFieldOptions))]
[JsonConverter(typeof(JsonSubtypes), "_t")]
public class FieldOptions
{
    [JsonProperty("_t")] 
    // ReSharper disable once InconsistentNaming
    public virtual string _t => GetType().Name;

    /// <summary>
    /// Link for field 
    /// {value} will be replaced with field value
    /// {id} will be replaced with id of the record
    /// </summary>
    public string LinkUrl { get; set; }
    
    /// <summary>
    /// Label used to present Link (template)
    /// TODO: to implement in the frontend 
    /// </summary>
    public string LinkLabel { get; set; }
}

public static class OpenApiFormatValues
{
    // Integer
    public const string Integer32 = "int32";
    public const string Integer64 = "int64";

    // Number
    public const string Float = "float";
    public const string Double = "double";

    // string
    public const string Byte = "byte";
    public const string Binary = "binary";
    public const string Date = "date";
    public const string DateTime = "date-time";
    public const string Password = "password";

    // string: not official
    public const string Email = "email";
    public const string UUID = "uuid";
    public const string URI = "uri";
    public const string URL = "url";
    public const string Hostname = "hostname";
    public const string IPV4 = "ipv4";
    public const string IPV6 = "ipv6";
}

[SwaggerSubType(typeof(LabelFieldOptions), DiscriminatorValue = nameof(LabelFieldOptions))]
[SwaggerSubType(typeof(URLFieldOptions), DiscriminatorValue = nameof(URLFieldOptions))]
public class TextFieldOptions : FieldOptions
{
    // TODO: make it "MultiLine" ...
    public bool? Multline { get; set; }

    /// <summary>
    /// Mime content type (only option supported right now is text/html)
    /// </summary>
    public string ContentType { get; set; }
    
    /// <summary>
    /// openapi: data format: uuid, ...
    /// </summary>
    public string Format { get; set; }
    
    /// <summary>
    /// openapi: pattern (RegEx)
    /// </summary>
    public string Pattern { get; set; }
    
    /// <summary>
    /// openapi: Maximum string length
    /// </summary>
    public int? MaxLength { get; set; }
    
    /// <summary>
    /// Whether this field allows using (handlebars) expression
    /// it will prevent its default value from being resolved 
    /// </summary>
    [Obsolete("wrap field with ExpressionField")]
    public bool AllowExpressions { get; set; }
}

public class ExpressionFieldOptions : FieldOptions
{
    public FormField ValueField { get; set; }
}

// TODO: divorce it form TextFieldOptions
// ...
public class URLFieldOptions : TextFieldOptions
{
}

public enum LabelStyle
{
    Normal,
    Header,
    Subheader,
    Subheader2,
    HTML,
    Button,
}

public enum PalletColor
{
    Default,
    Primary,
    Secondary,
    TextPrimary,
    TextSecondary,
    Error
}

public class LabelFieldOptions : TextFieldOptions
{
    public LabelStyle? Style { get; set; }
    public PalletColor? Color { get; set; }
}

public class SelectFieldOptionsBuilder : IEnumerable
{
    private OrderedDictionary _dict = new OrderedDictionary();
    public IEnumerator GetEnumerator() => _dict.Values.GetEnumerator();

    public SelectFieldOptionsBuilder()
    {
    }

    public SelectFieldOptionsBuilder(IEnumerable<KeyValuePair<string, string>> items)
    {
        foreach (var item in items) _dict.Add(item.Key, item.Value);
    }

    public void Add(string key, string value)
    {
        _dict.Add(key, value);
    }

    public static implicit operator SelectFieldOptions(SelectFieldOptionsBuilder builder)
        => new SelectFieldOptions
        {
            Items = builder._dict
        };

    public bool TryGetValue(string key, out string selected)
    {
        if (!_dict.Contains(key))
        {
            selected = null;
            return false;
        }

        selected = _dict[key] as string;
        return true;
    }

    public IEnumerable<string> Keys => _dict.Keys.OfType<string>();
    public IEnumerable<string> Values => _dict.Values.OfType<string>();
}

public class DictionaryFieldOptions : FieldOptions
{
    [Obsolete("embed field instead")] public string KeyFieldName { get; set; }

    [Obsolete("embed field instead")] public string ValueFieldName { get; set; }

    public bool ExpandAllKeys { get; set; }

    public FormField KeyField { get; set; }
    public FormField ValueField { get; set; }
}

public class ArrayFieldOptions : FieldOptions
{
    public FormField ValueField { get; set; }
}

/// <summary>
/// Field with Grid showing related/children "objects"
/// Will dynamically load children using the Url
/// </summary>
public class RelatedObjectsFieldOptions : FieldOptions
{
    /// <summary>
    /// DataView url
    /// {{id}} will be replaced with parent id
    /// </summary>
    public string Url { get; set; }

    public Condition[] Criteria { get; set; }
}

public class ObjectFieldOptions : FieldOptions
{
    public string ObjectType { get; set; }

    /// <summary>
    /// Only for the FE to be able to render it
    /// For object types with discriminators, Forms will be set instead of this.
    /// </summary>
    [BsonIgnore]
    public Form EditForm { get; set; }
    
    /// <summary>
    /// For objects with discriminator, one form for each type possible
    /// Key is the object type name of the child
    /// Value is a form with all the fields initialized to match the discriminated values for the object type
    /// When this is present Form will be null
    /// </summary>
    [BsonIgnore]
    public Dictionary<string, string> AddFormUrls { get; set; }    
}

public class LocationFieldOptions : FieldOptions
{
    // /// <summary>
    // /// Field used to determine icon
    // /// - can be a calculated field?
    // /// - should it automatically fallback to the first iconField in the card fields 
    // /// </summary>
    // public string IconFieldName { get; set; }
    
    // TODO: should it create an image field with the mapping and add to the card fields instead?
    // /// <summary>
    // /// Mapping between the value of the IconDiscriminator field and the icon image
    // /// icon image can be an URI or an "icon name"
    // /// </summary>
    // public Dictionary<string, string> Icons { get; set; }
    
    /// <summary>
    /// Fields to be used in card
    /// </summary>
    public FormField[] Fields { get; set; }
    
    /// <summary>
    /// layout for the card
    /// </summary>
    public FormLayout FormLayout { get; set; }
    
    public string IconFieldName { get; set; }
}

public class ChildrenFieldOptions : FieldOptions
{
    public const string IndexKeyType = "int";
    public const string StringKeyType = "string";

    /// <summary>
    /// DataView url
    /// {{id}} will be replaced with parent id
    /// </summary>
    public string Url { get; set; }

    // ????
    public Condition[] Criteria { get; set; }
    
    /// <summary>
    /// Object Type for children
    /// </summary>
    public string ObjectType { get; set; }

    /// <summary>
    /// int (array) or string (dictionary)
    /// </summary>
    public string KeyType { get; set; }
    
    /// <summary>
    /// For arrays, use the value of this (Child) field to represent child
    /// TODO: in the future, could be an expression using multiple field values
    /// (e.g. "{{FieldName}} {{Operator}} {{Value}}")
    /// ....
    /// </summary>
    public string DisplayExpression { get; set; }
    
    /// <summary>
    /// when defined, it is used instead of a simple text field
    /// </summary>
    public FormField KeyField { get; set; }

    // add/edit child in popup dialog?
    // public bool UsesPopup { get; set; }

    /// <summary>
    /// Only for the FE to be able to render it
    /// forms representing each object
    /// TODO: add alternative to load forms on demand (e.g. EditFormUrls) to speed up loading this field
    /// ...
    /// </summary>
    [BsonIgnore]
    public Dictionary<string, Form> EditForms { get; set; }
    
    /// <summary>
    /// For objects with discriminator, one form for each type possible
    /// Key is the object type name of the child
    /// Value is a form with all the fields initialized to match the discriminated values for the object type
    /// When this is present Form will be null
    /// </summary>
    [BsonIgnore]
    public Dictionary<string, string> AddFormUrls { get; set; }    
}

[Obsolete("Use ReferenceField")]
public class LookupFieldOptions : SelectFieldOptions
{
    public string Entity { get; set; }
    public DataQuery Query { get; set; } = new DataQuery();
    public string ValueProperty { get; set; }
    public string NameProperty { get; set; }
}

public class TagsFieldOptions : FieldOptions
{
    /// <summary>
    /// Lookup url
    /// {{id}} will be replaced with parent id
    /// </summary>
    public string Url { get; set; }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum NumberFieldOptionsStyle
{
    Default,

    /// <summary>
    /// decimal number representing currency (assumes US$ right now)
    /// </summary>
    Currency,

    /// <summary>
    /// Rating (1-5) "stars"
    /// </summary>
    Rating,

    /// <summary>
    /// Price (1-5) "$"
    /// </summary>
    Price
};

public class NumberFieldOptions : FieldOptions
{
    public static NumberFieldOptions Currency => new()
    {
        Style = NumberFieldOptionsStyle.Currency
    };

    public NumberFieldOptionsStyle Style { get; set; }
    public int? DecimalPlaces { get; set; }
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public decimal? MultipleOf { get; set; }
    public bool? ExcludeMinimum { get; set; }
    public bool? ExcludeMaximum { get; set; }

    // public string Locale { get; set; } = "en-US";
}

[JsonConverter(typeof(StringEnumConverter))]
public enum CheckboxFieldOptionsStyle
{
    Default,
    Toggle,
    Button,
    Dropdown,
};

public class CheckboxFieldOptions : FieldOptions
{
    public CheckboxFieldOptionsStyle Style { get; set; }
}