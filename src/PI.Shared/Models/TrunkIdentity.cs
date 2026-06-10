using System;

namespace PI.Shared.Models
{
    public class TrunkIdentity
    {
        public Guid EntityId { get; set; }
        public string Level { get; set; }
        public string Name { get; set; }
        public string IdentityProviderId { get; set; }
        public string ExternalId { get; set; }
    }
}