using System;
using Messages.Integration;

namespace Messages.Lead
{
    /// <summary>
    /// Lead Integration added to database
    /// </summary>
    public class LeadIntegration : IntegrationUpdate
    {
        public static string IntegrationRoute(Guid leadTypeId)
        {
            return $"lead.{leadTypeId}.integration";
        }
    }
}