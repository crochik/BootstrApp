using System;

namespace PI.Shared.Extensions;

public static class DateTimeExtensions
{
    public static DateTime StartOfYear(this DateTime date) => new DateTime(date.Year, 1, 1, 0, 0, 0, date.Kind);
}