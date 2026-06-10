using System;

namespace PI.Shared.Models
{
    public abstract class AppProfileElement : AppElement, IProfileElement
    {
        /// <summary>
        /// (optional) Use this version for these profiles
        /// </summary>
        public Guid[] ProfileIds { get; set; }

        /// <summary>
        /// optional Role 
        /// </summary>
        public EntityRoleId? Role { get; set; }
    }
}