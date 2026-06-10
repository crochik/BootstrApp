using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class PostalCodeField : FormField
{
    public override string Type => "postalCode";
    
    public override BackingType GetBackingType() => BackingType.String;

    // public override object AutoConvert(object value)
    // {
    //     // TODO: check options?
    //     
    //     
    //     return base.AutoConvert(value);
    // }
    //
    // public static string GetPostalCodeForLookup(string postalCode)
    // {
    //     if (string.IsNullOrWhiteSpace(postalCode)) return null;
    //     if (postalCode[0] >= '0' && postalCode[0] <= '9')
    //     {
    //         switch (postalCode.Length)
    //         {
    //             case < 4: return null;
    //             case > 5:
    //                 postalCode = postalCode[..5];
    //                 break;
    //         }
    //
    //         if (!int.TryParse(postalCode, out var num)) return null;
    //         postalCode = num.ToString();
    //         if (postalCode.Length < 4) return null;
    //         return postalCode.Length == 4 ? "0" + postalCode : postalCode;
    //     }
    //
    //     return postalCode.Length < 3 ? null : postalCode[..3].ToUpperInvariant();
    // }
    
    [JsonIgnore]
    [BsonIgnore]
    public PostalCodeFieldOptions PostalCodeFieldOptions
    {
        get => Options as PostalCodeFieldOptions;
        set => Options = value;
    }    
}

public class PostalCodeFieldOptions : FieldOptions
{
}
