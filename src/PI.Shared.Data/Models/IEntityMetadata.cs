using System;

namespace PI.Shared.Data.Models
{
    public interface IEntityMetadata
    {
        Guid EntityId { get; }
        Guid PartitionId { get; }
        string Key { get; }
        string Value { get; }
    }

    public class EntityMetadata : IEntityMetadata
    {
        public Guid EntityId { get; set; }

        public Guid PartitionId { get; set; }

        public string Key { get; set; }

        public string Value { get; set; }
    }
}
