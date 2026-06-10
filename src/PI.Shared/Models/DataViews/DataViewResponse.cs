using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Form.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace PI.Shared.Models;

public interface IDataViewResponse : IDataResponse
{
    DataViewRequest Request { get; }
    IEnumerable<object> Result { get; }
    DataView View { get; }
    string Message { get; }
    string NextUrl { get; }
}

[BsonKnownTypes(typeof(CardDataViewOptions), typeof(CalendarViewOptions), typeof(MapViewOptions), typeof(ImageGalleryViewOptions))]
[SwaggerDiscriminator("_t")]
[SwaggerSubType(typeof(CardDataViewOptions), DiscriminatorValue=nameof(CardDataViewOptions))]
[SwaggerSubType(typeof(CalendarViewOptions), DiscriminatorValue=nameof(CalendarViewOptions))]
[SwaggerSubType(typeof(MapViewOptions), DiscriminatorValue=nameof(MapViewOptions))]
[SwaggerSubType(typeof(ImageGalleryViewOptions), DiscriminatorValue=nameof(ImageGalleryViewOptions))]
public class DataViewOptions
{
    public static readonly DataViewOptions Default = new DataViewOptions();

    [BsonIgnore] 
    [JsonProperty("_t")] 
    // ReSharper disable once InconsistentNaming
    public string _t => GetType().Name;

    [BsonIgnore] [JsonProperty] public virtual string Type => DataViewComponent.Auto;
    
    /// <summary>
    /// Whether to hide the toolbar (title, filter, order, ...)
    /// </summary>
    public bool? HideToolbar { get; set; }
    
    /// <summary>
    /// (optional) Frontend Component name to be used
    /// </summary>
    public string Component { get; set; }
}

[BsonDiscriminator("imageGallery")]
public class ImageGalleryViewOptions : DataViewOptions
{
    public override string Type => DataViewComponent.ImageGallery;
    
    /// <summary>
    /// Field name with value for thumbnail Url (if omitted, will use imageUrl)
    /// </summary>
    public string ThumbnailUrl { get; set; }
    
    /// <summary>
    /// Field name with value for Image Url 
    /// </summary>
    public string ImageUrl { get; set; }
    
    /// <summary>
    /// field name with value for label
    /// </summary>
    public string Label { get; set; }
    
    public int? Width { get; set; }
    public int? Height { get; set; }
}

[BsonDiscriminator("card")]
public class CardDataViewOptions : DataViewOptions
{
    public static CardDataViewOptions Default => new CardDataViewOptions
    {
    };

    public override string Type => DataViewComponent.Card;
    
    /// <summary>
    /// Fields to be used in card
    /// </summary>
    public FormField[] Fields { get; set; }
    
    /// <summary>
    /// layout for the card
    /// </summary>
    public FormLayout FormLayout { get; set; }
    
    public bool? ShowLabels { get; set; }
}

// public class CalendarCardStyle
// {
//     public string Color { get; set; }
// }

public enum CalendarViewType
{
    Day,
    Week,
    SevenDays,
    MondayToFriday,
    Month,
    Agenda,
}

public class CalendarView
{
    public string Name { get; set; }
    public CalendarViewType Type { get; set; }
    public string Group { get; set; }
}

[BsonDiscriminator("calendar")]
public class CalendarViewOptions : DataViewOptions
{
    public override string Type => DataViewComponent.Calendar;

    public int? StartHour { get; set; }
    public int? EndHour { get; set; }

    // /// <summary>
    // /// what field to use to group (optional)
    // /// </summary>
    // public string GroupBy { get; set; }
    //
    // /// <summary>
    // /// what field to use for styling
    // /// </summary>
    // public string TypeField { get; set; }
    //
    // /// <summary>
    // /// conditional style based on value of TypeField
    // /// </summary>
    // public Dictionary<string, CalendarCardStyle> ConditionalStyle { get; set; }

    public CalendarView[] Views { get; set; }
}

[BsonDiscriminator("map")]
public class MapViewOptions : DataViewOptions
{
    public override string Type => DataViewComponent.Map;
}

public class DataViewResponse : IDataViewResponse
{
    public DataViewRequest Request { get; set; }
    public IEnumerable<object> Result { get; set; }
    public DataView View { get; set; }
    public string Message { get; set; }
    public string NextUrl { get; set; }

    public string ObjectType { get; set; }
    public Guid? Id { get; set; }
    
    // what was this for? 
    // public Guid? ObjectId { get; set; }

    public DataViewOptions Options { get; set; }

    public DataViewResponse UpdateFields()
    {
        if (Request == null || View?.Fields == null) return this;
        if (Request.Fields == null)
        {
            Request.Fields = View.Fields
                .Where(x => x is not HiddenField)
                .Where(x => x.IsVisible)
                .Select(x => x.Name)
                .ToArray();

            return this;
        }

        // reorder/hide fields in dataview
        View.Fields = getFields().ToArray();

        return this;

        IEnumerable<FormField> getFields()
        {
            var fields = View.Fields.ToDictionary(x => x.Name);
            foreach (var fieldName in Request.Fields)
            {
                if (fields.TryGetValue(fieldName, out var field))
                {
                    yield return field;
                }
            }

            var visible = Request.Fields.ToHashSet();
            foreach (var field in View.Fields)
            {
                if (visible.Contains(field.Name)) continue;
                field.Visible = new[] { "false" };
                yield return field;
            }
        }
    }
}