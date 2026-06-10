using System;

namespace PI.Shared.Models
{
    public static class NullableExtensions
    {
        public static object GetOptionalValue(this Guid? id) => id.HasValue ? (object)id.Value : null;
    }
}