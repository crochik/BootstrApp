using System;
using PI.Shared.Exceptions;

namespace PI.Shared.Form.Models;

public class DateRangePreset
{
    public string Name { get; set; }
    public string[] Range { get; set; }
    
    public static DateTime? Calculate(string value, TimeZoneInfo timeZoneInfo)
    {
        if (value == null) return null;
        if (value.StartsWith("{{") && value.EndsWith("}}"))
        {
            return CalculateExpression(value, timeZoneInfo);
        }

        if (DateTime.TryParse(value, out var dateTime))
        {
            return dateTime;
        }

        throw new BadRequestException($"Invalid value: {value}");
    }

    private static DateTime CalculateExpression(string value, TimeZoneInfo timeZoneInfo)
    {
        var tokens = value[2..^2].Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var localAnchor = GetAnchor(tokens[0], timeZoneInfo);

        return ApplyOffset(value, timeZoneInfo, tokens, localAnchor);
    }

    private static DateTime ApplyOffset(string value, TimeZoneInfo timeZoneInfo, string[] tokens, DateTime localAnchor)
    {
        var local = tokens.Length switch
        {
            1 => localAnchor,
            2 => ApplyOffset(localAnchor, tokens[1]),
            3 => ApplyOffset(localAnchor, tokens[1] + tokens[2]),
            _ => throw new BadRequestException($"Invalid expression: {value}")
        };

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZoneInfo);
    }

    /// <summary>
    /// Test method so one can control the anchor 
    /// </summary>
    public static DateTime CalculateExpressionWithoutAnchor(string value, TimeZoneInfo timeZoneInfo, DateTime localAnchor)
    {
        var tokens = value[2..^2].Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return ApplyOffset(value, timeZoneInfo, tokens, localAnchor);
    }

    private static DateTime ApplyOffset(DateTime anchor, string value)
    {
        if (value.Length < 3) throw new BadRequestException($"Invalid offset: {value}");
        var direction = value[0] switch
        {
            '+' => 1,
            '-' => -1,
            _ => throw new BadRequestException($"Invalid operation: {value[0]}")
        };

        if (!int.TryParse(value[1..^1], out var quantity))
        {
            throw new BadRequestException($"Invalid quantity: {value[1..^1]}");
        }

        switch (value[^1])
        {
            case 'M': //  month
            {
                var year = anchor.Year;
                var month = anchor.Month - 1 + (direction * quantity);
                year += month / 12;
                month %= 12;

                if (month < 0)
                {
                    year--;
                    month += 12;
                }

                month++;

                return new DateTime(year, month, anchor.Day, anchor.Hour, anchor.Minute, anchor.Second, anchor.Kind);
            }
            case 'y': // year
                return new DateTime(anchor.Year + direction * quantity, anchor.Month, anchor.Day, anchor.Hour, anchor.Minute, anchor.Second, anchor.Kind);
        }

        var off = value[^1] switch
        {
            'h' => TimeSpan.FromHours(quantity),
            'm' => TimeSpan.FromMinutes(quantity),
            'd' => TimeSpan.FromDays(quantity),
            _ => throw new BadRequestException($"Invalid unit: {value[^1]}"),
        };

        return direction < 0 ? anchor.Subtract(off) : anchor.Add(off);
    }

    private static DateTime GetAnchor(string token, TimeZoneInfo timeZoneInfo)
    {
        switch (token)
        {
            case "now":
            case "time":
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneInfo);

            case "today":
            case "day":
                return today();

            case "month":
                return firstOfMonth();

            case "year":
                return firstOfYear();
        }

        throw new BadRequestException($"Invalid anchor: {token}");

        DateTime localTime() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneInfo);
        DateTime today() => localTime().Date;

        DateTime firstOfMonth()
        {
            var local = localTime();
            return new DateTime(local.Year, local.Month, 1, 0, 0, 0, local.Kind);
        }

        DateTime firstOfYear()
        {
            var local = localTime();
            return new DateTime(local.Year, 1, 1, 0,0,0, local.Kind);
        }
    }
}

public class DateRangeFieldOptions : FieldOptions
{
    /// <summary>
    /// Min Range (e.g. 1d, 7d)
    /// </summary>
    public string Min { get; set; }

    /// <summary>
    /// Max Range (e.g. 30d, 1m)
    /// </summary>
    public string Max { get; set; }

    /// <summary>
    /// List of pre-sets
    /// </summary>
    public DateRangePreset[] Presets { get; set; }
}