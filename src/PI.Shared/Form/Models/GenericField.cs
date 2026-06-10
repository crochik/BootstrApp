using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class GenericField : FormField
{
    // TODO: add options 
    // if nothing else to specify what kind of backing
    // ...

    public override BackingType GetBackingType() => BackingType.Unknown;
    
    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        // do nothing
    }
    
    [JsonIgnore]
    [BsonIgnore]
    public GenericFieldOptions GenericFieldOptions
    {
        get => Options as GenericFieldOptions;
        set => Options = value;
    }    
}

public class GenericFieldOptions : FieldOptions
{
}
