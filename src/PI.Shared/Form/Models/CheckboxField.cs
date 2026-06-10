using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class CheckboxField : FormField
{
    // /// <summary>
    // /// temporary until the FE is updated
    // /// </summary>
    // public override string Type => "boolean";

    [JsonIgnore]
    [BsonIgnore]
    public CheckboxFieldOptions CheckboxFieldOptions
    {
        get => Options as CheckboxFieldOptions;
        set => Options = value as CheckboxFieldOptions;
    }
    
    public override object AutoConvert(object value)
    {
        if (value is string strValue)
        {
            if (string.IsNullOrEmpty(strValue)) return false;
            return bool.Parse(strValue);
        }

        // TBD
        return base.AutoConvert(value);
    }

    public override BackingType GetBackingType() => BackingType.Boolean;
}