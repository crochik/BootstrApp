using System;
using Crochik.Messaging;

namespace Messages.Integration
{
    public class IntegrationUpdate : IMessageBody
    {
        public Guid Id { get; set; }
        public Guid IntegrationId { get; set; }
        public string ExternalId { get; set; }
        public string Status { get; set; }
        public string Url { get; set; }
        public object Data { get; set; }
    }
}