using System;
using System.Collections.Generic;
using Crochik.Mongo;
using JsonSubTypes;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Swashbuckle.AspNetCore.Annotations;

namespace PI.Shared.Form.Models;

[DiscriminatorWithFallback]
[BsonDiscriminator(Required = true)]
[JsonConverter(typeof(JsonSubtypes), "type")]
[JsonSubtypes.KnownSubType(typeof(AddressField), "address")]
[JsonSubtypes.KnownSubType(typeof(ArrayField), "array")] // is it implemented? can it always be replaced by children multi-reference or multi-select 
[JsonSubtypes.KnownSubType(typeof(BitwiseFlagField), "bitwiseFlag")]
[JsonSubtypes.KnownSubType(typeof(CalculatedField), "calculated")]
[JsonSubtypes.KnownSubType(typeof(CheckboxField), "checkbox")] // used to be boolean 
[JsonSubtypes.KnownSubType(typeof(ChildrenField), "children")]
[JsonSubtypes.KnownSubType(typeof(DateField), "date")]
[JsonSubtypes.KnownSubType(typeof(DateRangeField), "dateRange")]
[JsonSubtypes.KnownSubType(typeof(DateTimeField), "datetime")]
[JsonSubtypes.KnownSubType(typeof(DictionaryField), "dictionary")]
[JsonSubtypes.KnownSubType(typeof(EmailField), "email")]
[JsonSubtypes.KnownSubType(typeof(ExpressionField), "expression")]
[JsonSubtypes.KnownSubType(typeof(FileField), "file")]
[JsonSubtypes.KnownSubType(typeof(GenericField), "generic")]
[JsonSubtypes.KnownSubType(typeof(HiddenField), "hidden")]
[JsonSubtypes.KnownSubType(typeof(ImageField), "image")]
[JsonSubtypes.KnownSubType(typeof(LabelField), "label")]
[JsonSubtypes.KnownSubType(typeof(LocationDistanceField), "distance")]
[JsonSubtypes.KnownSubType(typeof(LocationField), "location")]
[JsonSubtypes.KnownSubType(typeof(LookupField), "lookup")] // obsolete
[JsonSubtypes.KnownSubType(typeof(MultiReferenceField), "multiReference")]
[JsonSubtypes.KnownSubType(typeof(MultiSelectField), "multiSelect")]
[JsonSubtypes.KnownSubType(typeof(NumberField), "number")]
[JsonSubtypes.KnownSubType(typeof(ObjectField), "object")]
[JsonSubtypes.KnownSubType(typeof(PasswordField), "password")]
[JsonSubtypes.KnownSubType(typeof(PhoneField), "phone")]
[JsonSubtypes.KnownSubType(typeof(PostalCodeField), "postalCode")]
[JsonSubtypes.KnownSubType(typeof(PostalCodeField), "postalcode")]
[JsonSubtypes.KnownSubType(typeof(ReferenceField), "reference")]
[JsonSubtypes.KnownSubType(typeof(RelatedObjectsField), "relatedObjects")] // obsolete
[JsonSubtypes.KnownSubType(typeof(SelectField), "select")]
[JsonSubtypes.KnownSubType(typeof(TagsField), "tags")]
[JsonSubtypes.KnownSubType(typeof(TextField), "text")]
[JsonSubtypes.KnownSubType(typeof(TimeField), "time")]
[JsonSubtypes.KnownSubType(typeof(UrlField), "url")]
[SwaggerDiscriminator("_t")]
[SwaggerSubType(typeof(AddressField), DiscriminatorValue = "AddressField")]
[SwaggerSubType(typeof(ArrayField), DiscriminatorValue = "ArrayField")]
[SwaggerSubType(typeof(BitwiseFlagField), DiscriminatorValue = "BitwiseFlagField")]
[SwaggerSubType(typeof(CalculatedField), DiscriminatorValue = "CalculatedField")]
[SwaggerSubType(typeof(CheckboxField), DiscriminatorValue = "CheckboxField")]
[SwaggerSubType(typeof(ChildrenField), DiscriminatorValue = "ChildrenField")]
[SwaggerSubType(typeof(DateField), DiscriminatorValue = "DateField")]
[SwaggerSubType(typeof(DateRangeField), DiscriminatorValue = "DateRangeField")]
[SwaggerSubType(typeof(DateTimeField), DiscriminatorValue = "DateTimeField")]
[SwaggerSubType(typeof(DictionaryField), DiscriminatorValue = "DictionaryField")]
[SwaggerSubType(typeof(EmailField), DiscriminatorValue = "EmailField")]
[SwaggerSubType(typeof(ExpressionField), DiscriminatorValue = "ExpressionField")]
[SwaggerSubType(typeof(FileField), DiscriminatorValue = "FileField")]
[SwaggerSubType(typeof(GenericField), DiscriminatorValue = "GenericField")]
[SwaggerSubType(typeof(HiddenField), DiscriminatorValue = "HiddenField")]
[SwaggerSubType(typeof(ImageField), DiscriminatorValue = "ImageField")]
[SwaggerSubType(typeof(LabelField), DiscriminatorValue = "LabelField")]
[SwaggerSubType(typeof(LocationDistanceField), DiscriminatorValue = "DistanceField")]
[SwaggerSubType(typeof(LocationField), DiscriminatorValue = "LocationField")]
[SwaggerSubType(typeof(LookupField), DiscriminatorValue = "LookupField")]
[SwaggerSubType(typeof(MultiReferenceField), DiscriminatorValue = "MultiReferenceField")]
[SwaggerSubType(typeof(MultiSelectField), DiscriminatorValue = "MultiSelectField")]
[SwaggerSubType(typeof(NumberField), DiscriminatorValue = "NumberField")]
[SwaggerSubType(typeof(ObjectField), DiscriminatorValue = "ObjectField")]
[SwaggerSubType(typeof(PasswordField), DiscriminatorValue = "PasswordField")]
[SwaggerSubType(typeof(PhoneField), DiscriminatorValue = "PhoneField")]
[SwaggerSubType(typeof(PostalCodeField), DiscriminatorValue = "PostalCodeField")]
[SwaggerSubType(typeof(PostalCodeField), DiscriminatorValue = "PostalCodeField")]
[SwaggerSubType(typeof(ReferenceField), DiscriminatorValue = "ReferenceField")]
[SwaggerSubType(typeof(RelatedObjectsField), DiscriminatorValue = "RelatedObjectsField")]
[SwaggerSubType(typeof(SelectField), DiscriminatorValue = "SelectField")]
[SwaggerSubType(typeof(TagsField), DiscriminatorValue = "TagsField")]
[SwaggerSubType(typeof(TextField), DiscriminatorValue = "TextField")]
[SwaggerSubType(typeof(TimeField), DiscriminatorValue = "TimeField")]
[SwaggerSubType(typeof(UrlField), DiscriminatorValue = "UrlField")]
public class FormField : UIElement
{
    private static readonly CamelCaseNamingStrategy ApiNamingStrategy = new CamelCaseNamingStrategy();

    /// <summary>
    /// Exposes _t (discriminator) to api client and uses it when mapping into ExpandoObject to edit using form
    /// Mongo will serialize it automatically based on the discriminator
    /// </summary>
    [JsonProperty("_t")]
    // ReSharper disable once InconsistentNaming
    public string _t => GetType().Name;

    /// Only used for serialization into json
    /// overriden by RelatedObjects and CheckBox for backwards compatibility until FE is updated
    [BsonIgnore]
    [JsonProperty("type")]
    public override string Type
    {
        get
        {
            var name = GetType().Name.ToLowerInvariant();
            return name[..^5];
        }
    }

    /// <summary>
    /// Description (for user)
    /// </summary>
    public string Description { get; set; }

    public object DefaultValue { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>
    /// API name used for the property
    /// </summary>
    public string ApiName { get; set; }

    public static string GetDefaultApiName(string name) => ApiNamingStrategy.GetPropertyName(name, false);
    
    /// <summary>
    /// is it used? probably shouldn't be... would make more sense to be part of the options so it can be strongly typed
    /// </summary>
    [Obsolete]
    public object Style { get; set; }

    public FieldOptions Options { get; set; }

    public virtual BackingType GetBackingType() => BackingType.Unknown;
    public virtual object AutoConvert(object value) => GetBackingType().AutoConvert(value);

    // TODO: should it take IEntityContext?
    // ...
    public virtual void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        if (DefaultValue is string defaultValue)
        {
            if (defaultValue.StartsWith("#{{") && defaultValue.EndsWith("}}"))
            {
                // calculated client side?
                // ...
            }
            else if (!ExpressionEvaluatorService.TryResolve(context, objectContext, defaultValue, out var resolvedValue))
            {
                // failed
                DefaultValue = null;
            }
            else
            {
                DefaultValue = resolvedValue;
            }
        }

        // TODO: partial substitutions in Options.LinkUrl
        // ...
        // if (!string.IsNullOrEmpty(Options.LinkUrl)&& Options.LinkUrl.StartsWith("{{") && Options.LinkUrl.EndsWith("}}"))
        // {
        //     
        // }
    }

    public virtual void SetDefaultValue(Condition[] conditions)
    {
        if (conditions?.Length != 1) return;

        //  || (conditions[0].Operator != Operator.Eq && conditions[0].Operator != Operator.In)

        DefaultValue = conditions[0].Value;
    }

    public virtual IEnumerable<string> GetDependencies(bool forCalculation = false, bool requiredOutput = false)
    {
        yield break;
    }

    /// <summary>
    /// Get the path to the property in the (mongo) collection
    /// </summary>
    /// <returns></returns>
    public string GetPathInCollection() => GetPathInCollection(Name);

    /// <summary>
    /// Get the path to the property in the (mongo) collection
    /// </summary>
    /// <returns></returns>
    public static string GetPathInCollection(string fieldName) => fieldName.Replace('|', '.');
}