using System;
using Crochik.Messaging;

namespace Messages.Integration
{
    public class UpsertIntegration : IMessageBody
    {
        public string ObjectType { get; set; }
        public IntegrationUpdate Integration { get; set; }

        public static string UpsertLead(Guid integrationId)
        {
            return $"integration.{integrationId}.upsertLead";
        }

        public static string UpsertAppointment(Guid integrationId)
        {
            return $"integration.{integrationId}.upsertAppointment";
        }
    }
}