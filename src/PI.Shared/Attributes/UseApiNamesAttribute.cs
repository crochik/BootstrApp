using System;

namespace PI.Shared.Attributes;

/// <summary>
/// Denotes (api) actions that should override the serialization in the DynamicJsonSerializationFilter
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class UseApiNamesAttribute : Attribute { }