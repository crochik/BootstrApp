using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Crochik.Mongo;
using JsonSubTypes;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Models.Expressions;
using Swashbuckle.AspNetCore.Annotations;

namespace PI.Shared.Models.Layout;


public class LayoutItemCssStyle
{
    public string MinWidth { get; set; }
    public string MaxWidth { get; set; }
    public string MinHeight { get; set; }
    public string MaxHeight { get; set; }
}

// bson
[DiscriminatorWithFallback]
[BsonDiscriminator(Required = true)]
[BsonKnownTypes(
    typeof(LayoutContainer),
    typeof(ObjectLayoutItem)
)]
    
// api
[SwaggerDiscriminator("_t")]
[SwaggerSubType(typeof(LayoutContainer), DiscriminatorValue=nameof(LayoutContainer))]
[SwaggerSubType(typeof(ObjectLayoutItem), DiscriminatorValue=nameof(ObjectLayoutItem))]

[JsonConverter(typeof(JsonSubtypes), "_t")]
[JsonSubtypes.KnownSubType(typeof(LayoutContainer), "Container")]
[JsonSubtypes.KnownSubType(typeof(ObjectLayoutItem), "Item")]
// [JsonSubtypes.KnownSubType(typeof(FieldLayoutItem), "Field")]
public class LayoutItem
{
    [JsonProperty("_t")]
    // ReSharper disable once InconsistentNaming
    public virtual string _t => GetType().Name;

    public string Name { get; set; }
    public string Label { get; set; }
    public int? Weight { get; set; }
    
    public LayoutItemCssStyle Style { get; set; }

    public virtual bool FillPlaceholders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        return true;
    }
}

// DO NOT SPECIFY the converter or we won't be able to override it later
// - for controllers the StringEnumConverter is the default anyway
// - we need to be able to make it ignore the EnumMember attribute or 
//      we can't have it serialize as mongo
// [JsonConverter(typeof(StringEnumConverter))]
public enum ScreenBreakpoint
{
    // xs, extra-small: 0px
    // sm, small: 600px 4
    // md, medium: 900px 6
    // lg, large: 1200px 8
    // xl, extra-large: 1536px 12
    
    [EnumMember(Value = "xs")]
    ExtraSmall,
    
    [EnumMember(Value = "sm")]
    Small, 
    
    [EnumMember(Value = "md")]
    Medium, 
    
    [EnumMember(Value = "lg")]
    Large, 
    
    [EnumMember(Value = "xl")]
    ExtraLarge,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum LayoutContainerType
{
    Row,
    Column, 
    Tabs
}


[JsonConverter(typeof(StringEnumConverter))]
public enum LayoutJustify
{
    Start,
    End, 
    Between,
}
    
[BsonDiscriminator("Container")]
public class LayoutContainer : LayoutItem
{
    public LayoutContainerType Type { get; set; }
    public LayoutItem[] Children { get; set; }
    public int? Spacing { get; set; }
    public LayoutJustify Justify { get; set; }
    
    public override bool FillPlaceholders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        return Children.All(child => child.FillPlaceholders(context, objectContext));
    }
}

[BsonDiscriminator("Item")]
public class ObjectLayoutItem : LayoutItem
{
    public string Url { get; set; }
    public bool LazyLoad { get; set; }
    
    public override bool FillPlaceholders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        if (!ExpressionEvaluatorService.TryResolve(context, objectContext, Url, out var resolved) || resolved is not string url)
        {
            return false;
        }

        Url = url;
        return true;
    }
}