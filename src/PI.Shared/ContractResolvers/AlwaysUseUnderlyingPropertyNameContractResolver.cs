using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PI.Shared.ContractResolvers;

/// <summary>
/// Contract resolver that will always use the property name, regardless of the JsonProperty (for example)
/// TODO: could have it use the BsonElement
/// ...
/// </summary>
public class AlwaysUseUnderlyingPropertyNameContractResolver : DefaultContractResolver
{
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        var list = base.CreateProperties(type, memberSerialization);
        foreach (JsonProperty prop in list)
        {
            prop.PropertyName = prop.UnderlyingName;
        }
    
        return list;
    }
}