using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PI.Shared.ContractResolvers;

/// <summary>
/// Serialize "all" properties (including the ones marked with JsonIgnore) and
/// use UnderlyingName
/// </summary>
public class AllPropertiesWithUnderlyingNameContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        // Check if the property has a [JsonIgnore] attribute
        if (member.GetCustomAttribute<JsonIgnoreAttribute>() != null)
        {
            // If it does, we can force it to be serialized
            property.Ignored = false;
            property.ShouldSerialize = instance => true; // Ensure it's always serialized
        }
        
        // reset property name to UnderlyingName
        property.PropertyName = property.UnderlyingName;

        return property;
    }
}