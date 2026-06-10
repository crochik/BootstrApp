using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class ExpressionField : FormField
{
    public override BackingType GetBackingType() => BackingType.String;

    [JsonIgnore]
    [BsonIgnore]
    public ExpressionFieldOptions ExpressionFieldOptions
    {
        get => Options as ExpressionFieldOptions;
        set => Options = value;
    }
    
    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        // TODO: it may need to be selective and apply to the value field partially (excluding value)
        // for example, when the value field is a objectField, referenceField, ... and there are conditions based on a expression
        // ...
        
        // for now, do nothing
    }
    
    public override object AutoConvert(object value)
    {
        if (value is string) return value;
        return ExpressionFieldOptions?.ValueField!=null ? ExpressionFieldOptions?.ValueField.AutoConvert(value) : value;
    }
}