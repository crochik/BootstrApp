using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class PasswordField : FormField
{
    public override BackingType GetBackingType() => BackingType.String;

    [JsonIgnore]
    [BsonIgnore]
    public PasswordFieldOptions PasswordFieldOptions
    {
        get => Options as PasswordFieldOptions;
        set => Options = value;
    }
}

public class PasswordFieldOptions : FieldOptions
{
    public DataProtectionConfig DataDataProtection { get; set; }
    
    /// <summary>
    /// Odd but the idea is that a multiline password would extend it into a "secret"
    /// </summary>
    public bool Multiline { get; set; }
    
    /// <summary>
    /// openapi: pattern (RegEx)
    /// </summary>
    public string Pattern { get; set; }
    
    /// <summary>
    /// openapi: Maximum string length
    /// </summary>
    public int? MaxLength { get; set; }    
}

[BsonDiscriminator(Required = true)]
[BsonKnownTypes(typeof(MicrosoftDataProtectionConfig))]
public abstract class DataProtectionConfig
{
    public abstract string ProviderName { get; }
}

[BsonDiscriminator("ms-data-protection")]
public class MicrosoftDataProtectionConfig : DataProtectionConfig
{
    public override string ProviderName => "MicrosoftDataProtection";
    public string Purpose { get; set; }
}

