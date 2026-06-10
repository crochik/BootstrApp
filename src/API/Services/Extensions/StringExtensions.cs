namespace Services
{
    public static class StringExtensions
    {
        public static string Or(this string str, string defaultValue)
            => string.IsNullOrWhiteSpace(str) ? defaultValue : str;
    }
}