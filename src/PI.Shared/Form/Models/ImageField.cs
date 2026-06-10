using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Form.Models;

public class ImageField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public ImageFieldOptions ImageFieldOptions
    {
        get => Options as ImageFieldOptions;
        set => Options = value;
    }
}

public class ImageFieldOptions : FieldOptions
{
    public const string EmptyImage = "#null";
    public const string NotFoundImage = "#not-found";
    
    public Dictionary<string, string> ImageUris { get; set; }
    public string UriTemplate { get; set; }
    public int? IconSize { get; set; }
    public int? PreviewSize { get; set; }
}