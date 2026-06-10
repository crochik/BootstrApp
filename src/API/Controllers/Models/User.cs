using System;
using PI.Shared.Models;

namespace Controllers.Models
{
    public class User
    {
        public Guid? Id { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? AccountId { get; set; }
        public EntityRoleId Role { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public Identity[] Identities { get; set; }
        public string Email { get; set; }
        public string TimeZoneId { get; set; }
    }

    public class Organization
    {
        public Guid? Id { get; set; }
        public string Name { get; set; }
        public Guid? AccountId { get; set; }
        public bool IsActive { get; set; }
        public Identity[] Identities { get; set; }
        public string TimeZoneId { get; set; }
    }

    // public class EntityGroup : IEntityGroup
    // {
    //     public Guid AccountId { get; set; }

    //     public string Name { get; set; }
    //     public string Description { get; set; }

    //     public Guid Id { get; set; }
    //     public Guid EntityId { get; set; }
    // }
}
