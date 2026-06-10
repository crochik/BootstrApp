using System;
using System.Text.RegularExpressions;

namespace PI.Shared.Models;

public class PersonName
{
    public string FirstName { get; init;  }
    public string LastName { get; init; }
    public string Raw { get; init;  }

    public static bool TryParse(string fullName, out PersonName parsed)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            parsed = null;
            return false;
        }

        // wife and husband
        // ...

        // var words = fullName.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        // if (words.Length > 1)
        // {
        //     // can be "lastName, first middle" or some suffix (Jr. Sr. Phd ...)
        // }

        var words = fullName.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        parsed = new PersonName
        {
            FirstName = words[0],
            LastName = words.Length > 1 ? words[^1] : null,
            Raw = fullName,
        };

        return true;
    }

    // different attempt, not used/finished
    // TODO: handle Suffixes (e.g. Jr., Sr., ....)
    // TODO: Phd, ....
    private static string[] SplitName(string name)
    {
        Match m = Regex.Match(name, @"^([a-zA-Z]+),\s?([a-zA-Z]+)\s?([a-zA-Z\s]*)?$");
        string[] ret;

        if (m.Success)
        {
            ret = new string[] { m.Groups[2].Value, m.Groups[1].Value, m.Groups[3].Value };
        }
        else
        {
            m = Regex.Match(name, @"^([a-zA-Z]+)\s?([a-zA-Z\s]+\s)?([a-zA-Z]+)?$");
            if (m.Success)
            {
                ret = new string[] { m.Groups[1].Value, m.Groups[3].Value, m.Groups[2].Value };
            }
            else
            {
                ret = new string[] { name, null, null };
            }
        }

        for (var c = 0; c < ret.Length; c++)
        {
            if (ret[c] == null) continue;
            ret[c] = ret[c].Trim();
            if (ret.Length == 0) ret[c] = null;
        }

        return ret;
    }
}