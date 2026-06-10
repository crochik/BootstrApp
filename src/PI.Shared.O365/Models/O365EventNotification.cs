using System;
using Crochik.Messaging;

namespace PI.Shared.Models
{
    public class O365EventNotification : IMessageBody
    {
        public Guid AccountId { get; set; }
        public Guid? UserId { get; set; }
        public string ChangeType { get; set; }
        public string Resource { get; set; }
        public string SubscriptionId { get; set; }
    }
}
