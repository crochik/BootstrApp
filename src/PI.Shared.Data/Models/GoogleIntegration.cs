namespace PI.Shared.Data.Models;

public class GoogleIntegration
{
    public class Data
    {
        public string ChatSpaceKey { get; set; }
        public string ChatSpaceId { get; set; }
    }

    public class Auth
    {
        public string ChatSpaceToken { get; set; }
    }
}