using System;
using Crochik.Mongo;

namespace PI.Shared.Models
{
    [BsonCollection("o365.Subscription")]
    public class O365Subscription : EntityOwnedModel
    {
        public string NotificationUrl { get; set; }
        public string Resource { get; set; }
        public DateTime ExpiresOn { get; set; }
    }    
}