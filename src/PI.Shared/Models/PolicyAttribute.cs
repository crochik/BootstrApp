// using System;
//
// namespace PI.Shared.Models;
//
// [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
// public class PolicyAttribute : Attribute
// {
//     public Policy Policy { get; }
//     public EntityRoleId Level { get; }
//
//     public PolicyAttribute(
//         EntityRoleId level,
//         EntityRoleId ownerRole,
//         Permission owner = Permission.All,
//         Permission group = Permission.None,
//         Permission guest = Permission.None)
//     {
//         Level = level;
//         Policy = new Policy(ownerRole, owner, group, guest);
//     }
//
//     public PolicyAttribute(
//         EntityRoleId role,
//         Permission owner = Permission.All,
//         Permission group = Permission.None,
//         Permission guest = Permission.None)
//     {
//         Level = role;
//         Policy = new Policy(role, owner, group, guest);
//     }
// }