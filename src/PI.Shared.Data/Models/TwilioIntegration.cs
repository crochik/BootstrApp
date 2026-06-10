namespace PI.Shared.Data.Models;

public abstract class TwilioIntegration
{
    public class Data
    {
        public string AccountSid { get; set; }
        public string MessagingServiceSid { get; set; }
        public string PhoneNumber { get; set; }
        
        public string StatusCallbackUrl { get; set; }
    }
    
    public class Authentication
    {
        public string Token { get; set; }
    }
}