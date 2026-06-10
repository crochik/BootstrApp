using System;
using PI.Shared.Models;

namespace PI.Shared.Data.Models
{
    public class MergeUserCandidate
    {
        public Guid IdentityId { get; set; }
        public Guid EntityId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public EntityRoleId Role { get; set; }
        public string Context { get; set; }
    }

    public class MergeUserCandidateMatch
    {
        public MergeUserCandidate User1 { get; set; }
        public MergeUserCandidate User2 { get; set; }
    }
}
