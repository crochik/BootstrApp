namespace PI.Shared.Models
{
    public class EntityTrunkIntegration : EntityIntegration, IEntityTrunkIntegration
    {
        public EntityTrunkLevel Level { get; set; }
    }
}