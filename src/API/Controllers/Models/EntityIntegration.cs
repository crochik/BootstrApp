using System;

namespace Controllers.Models
{
    public class EntityIntegration
    {
        public string Name { get; set; }

        public Guid EntityId { get; set; }
        public Guid IntegrationId { get; set; }
        public bool Enabled { get; set; }
    }
}