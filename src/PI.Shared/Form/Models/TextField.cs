using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class TextField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public TextFieldOptions TextFieldOptions
    {
        get => Options as TextFieldOptions;
        set => Options = value;
    }

    public TextField()
    {
        TextFieldOptions = new TextFieldOptions();
    }

    public override BackingType GetBackingType() => BackingType.String;

    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        if (TextFieldOptions?.AllowExpressions ?? false) return;
        
        base.FillPlaceHolders(context, objectContext);
    }
}