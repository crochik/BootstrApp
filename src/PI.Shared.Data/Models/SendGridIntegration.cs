namespace PI.Shared.Data.Models;

public abstract class SendGridIntegration
{
    public class Data
    {
        public string FromName { get; set; }
        public string FromEmail { get; set; }
        public string TemplateId { get; set; }
        
        /// <summary>
        /// public key used to verify signature for webhook events
        /// </summary>
        public string WebhookSignatureKey { get; set; }
        
        /// <summary>
        /// Template for unsubscribe url (e.g. https://go.fci.cloud/unsubscribe/{{id}})
        /// </summary>
        public string UnsubscribeUrlTemplate { get; set; }
        
        /// <summary>
        /// Url to redirect after successful unsubscribe
        /// </summary>
        public string UnsubscribedRedirectUrl { get; set; }
    }

    public class Authentication
    {
        public string APIKey { get; set; }
    }
}