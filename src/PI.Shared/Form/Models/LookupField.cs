using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Form.Models;

[Obsolete]
public class LookupField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public LookupFieldOptions LookupFieldOptions
    {
        get => Options as LookupFieldOptions;
        set => Options = value;
    }

    public LookupField()
    {
        LookupFieldOptions = new LookupFieldOptions();
    }
}