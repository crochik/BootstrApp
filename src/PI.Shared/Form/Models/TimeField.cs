using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class TimeField : FormField
{
    public override object AutoConvert(object value)
    {
        if (value is string strValue)
        {
            return DateTime.Parse(strValue);
        }

        // TBD
        return value;
    }

    public override BackingType GetBackingType() => BackingType.DateTime;
    
    [JsonIgnore]
    [BsonIgnore]
    public TimeFieldOptions TimeFieldOptions
    {
        get => Options as TimeFieldOptions;
        set => Options = value;
    }    
}

public class TimeFieldOptions : FieldOptions
{
}