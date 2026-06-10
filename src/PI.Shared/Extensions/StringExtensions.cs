using System;
using System.Text;

namespace PI.Shared.Extensions;

public static class StringExtensions
{
    public static string ToCamelCase(this string str)
    {
        return string.IsNullOrEmpty(str) ? str :
            (str.Length > 1 ?
                Char.ToLowerInvariant(str[0]) + str.Substring(1) :
                str.ToLowerInvariant()
            );
    }

    public static string ToJSName(this string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var str = new StringBuilder();
        var nextUpper = false;
        var lastUpper = false;
        foreach (char letter in value)
        {
            if (letter >= 'a' && letter <= 'z')
            {
                char c = nextUpper ? (char)(letter + 'A' - 'a') : letter;
                str.Append(c);
                nextUpper = false;
                lastUpper = false;
            }
            else if (letter >= 'A' && letter <= 'Z')
            {
                nextUpper |= (str.Length > 0 && !lastUpper); // allow one upper case after non-upper case
                char c = nextUpper ? letter : (char)(letter - 'A' + 'a');
                str.Append(c);
                nextUpper = false;
                lastUpper = true;
            }
            else if (letter >= '0' && letter <= '9')
            {
                str.Append(letter);
                nextUpper = false;
                lastUpper = false;
            }
            else
            {
                nextUpper = true;
                lastUpper = false;
            }
        }

        return str.ToString();
    }

    public static Guid CalculateMD5Hash(this string str)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash= md5.ComputeHash(Encoding.UTF8.GetBytes(str));
        return new Guid(hash);
    }
}