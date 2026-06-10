using System;

namespace PI.Shared.Attributes;

/// <summary>
/// Override the generated Property Name
/// - only when the Use Api Names behavior is opted in (DynamicJsonSerializationFilter)
/// - similar to the JsonProperty
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ApiNameAttribute(string name) : Attribute
{
    public string Name { get; set; } = name;
}