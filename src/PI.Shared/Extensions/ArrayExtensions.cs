using System;
using System.Collections.Generic;
using System.Linq;

namespace PI.Shared.Extensions
{
    public static class ArrayExtensions
    {
        public static T[] Append<T>(this T[] array, T obj)
        {
            if (array == null)
            {
                return obj != null ? new T[] { obj } : Array.Empty<T>();
            }

            return obj == null ? array : ((IEnumerable<T>)array).Append(obj).ToArray();
        }
    }
}