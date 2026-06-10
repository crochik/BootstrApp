using System;

namespace PI.Shared.Models
{
    public class ObjectStatus : EntityOwnedModel, ILeadStatus
    {
        public Guid ObjectTypeId { get; set; }
        public string ObjectType { get; set; }
    }
}
