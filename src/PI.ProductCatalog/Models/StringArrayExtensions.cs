using System.Linq;

namespace PI.ProductCatalog.Models;

public static class StringArrayExtensions
{
    public static bool Contains(this string[] tags, string tag) => tags?.Any(x => string.Equals(x, tag)) ?? false;
    public static string[] Remove(this string[] tags, string tag)
    {
        if (tags != null)
        {
            tags = tags.Where(x => !string.Equals(x, tag)).ToArray();
            if (tags.Length == 0) tags = null;
        }

        return tags;
    }

    public static string[] Set(this string[] tags, string tag, bool set = true)
    {
        if (!set)
        {
            return Remove(tags, tag);
        }

        return (tags ?? Enumerable.Empty<string>()).Append(tag).Distinct().ToArray();
    }
}