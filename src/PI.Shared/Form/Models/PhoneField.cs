using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class PhoneField : FormField
{
    [BsonIgnore]
    [JsonIgnore]
    public PhoneFieldOptions PhoneFieldOptions
    {
        get => Options as PhoneFieldOptions;
        set => Options = value;
    }
    
    public override BackingType GetBackingType() => BackingType.String;

    public override object AutoConvert(object value)
    {
        value = base.AutoConvert(value);

        if (value is not string str) return value;
        if (PhoneFieldOptions == null || PhoneFieldOptions.AutoFormat == PhoneFieldOptions.AutoFormatOption.None) return value;
        if (!PhoneNumber.TryParse(str, out var phoneNumber)) return value;
        
        return PhoneFieldOptions.AutoFormat switch
        {
            PhoneFieldOptions.AutoFormatOption.International => phoneNumber.International,
            PhoneFieldOptions.AutoFormatOption.National => phoneNumber.Display,
            _ => value
        };
    }
}