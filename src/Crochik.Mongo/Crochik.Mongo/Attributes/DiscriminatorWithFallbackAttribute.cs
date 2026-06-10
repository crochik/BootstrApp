using System;

namespace Crochik.Mongo;

/// <summary>
/// If the a mapping for the discriminator value is not found will fallback to the base type
/// Allow new discriminator values to be introduced without breaking old binaries trying to deserialize them
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DiscriminatorWithFallbackAttribute : Attribute
{
}
