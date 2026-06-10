using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PI.Shared.Attributes;

namespace PI.Shared.ContractResolvers;

/// <summary>
/// Define Api Name (to match object type in the database)
/// - if finds [ApiNameAttribute] will use it
/// - if not, it will fall back to Property Name (UnderlyingName)
/// </summary>
public class ApiNamePropertyNameContractResolver : DefaultContractResolver
{
    public bool OverrideIgnoreAttribute { get; init; }
    
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        // Check if the property has a [JsonIgnore] attribute
        if (OverrideIgnoreAttribute && member.GetCustomAttribute<JsonIgnoreAttribute>() != null)
        {
            // If it does, we can force it to be serialized
            property.Ignored = false;
            property.ShouldSerialize = instance => true; // Ensure it's always serialized
        }

        var apiNameAttrib = member.GetCustomAttribute<ApiNameAttribute>();
        if (apiNameAttrib != null)
        {
            // override the generated name
            property.PropertyName = apiNameAttrib.Name;
            return property;
        }
        
        // reset property name to UnderlyingName
        property.PropertyName = property.UnderlyingName;

        return property;
    }    
}