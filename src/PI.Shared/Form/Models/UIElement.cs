using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Form.Models;

public class UIElement
{
    [JsonProperty("type")] [BsonIgnore] public virtual string Type => GetType().Name;

    public string Name { get; set; }
    public string Label { get; set; }
    public string[] Enable { get; set; }
    public string[] Visible { get; set; }

    /// <summary>
    /// Calculated property to help the FE :)
    /// </summary>
    public virtual bool IsReadOnly => Enable?.Any(x => x == "false") ?? false;

    /// <summary>
    /// Calculated property to help the FE :)
    /// </summary>
    public virtual bool IsVisible => !(Visible?.Any(x => x == "false") ?? false);
}