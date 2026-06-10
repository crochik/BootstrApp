using System.Linq;

namespace System.Collections.Generic
{
    public static class SystemCollectionGeneric
    {
        public static bool IsEmpty<T>(this IEnumerable<T> enumerable) 
            => enumerable == null || !enumerable.Any();
    }
}