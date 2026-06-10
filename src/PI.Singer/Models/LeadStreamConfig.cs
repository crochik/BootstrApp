using System;

namespace Models
{
    public class LeadStreamConfig : SingerStreamConfig
    {
        public Guid IntegrationId { get; set; }
        public Guid LeadTypeId { get; set; }

        public LeadStreamConfig()
        {
        }

        public LeadStreamConfig(Guid id)
        {
            LeadTypeId = id;
        }
    }
}