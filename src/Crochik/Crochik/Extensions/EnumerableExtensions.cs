using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Crochik.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> AsEnumerable<T>(this T single)
    {
        yield return single;
    }

    public static IEnumerable<T> AsEnumerable<T, T1>(this T1 single) where T1 : T
    {
        yield return single;
    }

    public static IEnumerable<object> ToEnumerableObject(this IEnumerable e)
    {
        foreach (var obj in e)
        {
            yield return obj;
        }
    }
}

public static class DecimalExtensions 
{
    public static readonly CultureInfo DefaultCultureInfo = new CultureInfo("en-US", false);
        
    public static string FormatCurrency(this decimal? value)
        => value.HasValue ? value.Value.FormatCurrency() : "[N/A]";

    public static string FormatCurrency(this decimal value)
        => string.Format(DefaultCultureInfo, "{0:C}", value);
}