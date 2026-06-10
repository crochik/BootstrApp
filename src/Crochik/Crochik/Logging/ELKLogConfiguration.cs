namespace Crochik.Logging
{
    public class ELKLogConfiguration
    {
        private static ELKLogConfiguration _instance = null;
        public static bool IsEnabled => _instance?.UseForLogging ?? false;

        public bool UseForLogging { get; set; }
        public string Url { get; set; }
        public string IndexFormat { get; set; } = "development";
        public string Authorization { get; set; }

        public ELKLogConfiguration()
        {
            UseForLogging = false;
            _instance = this; // hack
        }
    }
}