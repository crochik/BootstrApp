// using System;
// using FluentAssertions;
// using PI.Shared.Constants;
// using PI.Shared.Exceptions;
// using PI.Shared.Models;
// using Xunit;
//
// namespace UnitTests.Permissions
// {
//     public class PolicyTests
//     {
//         [Fact]
//         public void Create_Account()
//         {
//             var context = new AccountContext(AccountIds.FCI);
//             context.CanCreate<Account>().Should().BeFalse();
//             context.CanCreate<Organization>().Should().BeTrue();
//             context.CanCreate<User>().Should().BeTrue();
//
//             var org = context.Create<Organization>();
//             org.AccountId.Should().Be(context.AccountId.Value);
//             context.EvaluateRelationShip(org).Should().Be(AccessLevel.Owner);
//
//             var admin = context.Create<User>();
//             admin.UserRoleId = nameof(EntityRoleId.Admin);
//             admin.AccountId.Should().Be(context.AccountId.Value);
//             admin.EntityId.Should().Be(context.AccountId.Value);
//             context.EvaluateRelationShip(admin).Should().Be(AccessLevel.Owner);
//
//             admin.Context.EvaluateRelationShip(org).Should().Be(AccessLevel.Owner);
//
//             // ???
//             org.Context.EvaluateRelationShip(admin).Should().Be(AccessLevel.Group);
//         }
//
//         [Fact]
//         public void Create_Admin()
//         {
//             var context = UserContext.Admin(Guid.NewGuid(), "Admin", AccountIds.FCI);
//             context.CanCreate<Account>().Should().BeFalse();
//             context.CanCreate<Organization>().Should().BeTrue();
//             context.CanCreate<User>().Should().BeTrue();
//
//             var org = context.Create<Organization>();
//             org.AccountId.Should().Be(context.AccountId.Value);
//             context.EvaluateRelationShip(org).Should().Be(AccessLevel.Owner);
//
//             var admin = context.Create<User>();
//             admin.AccountId.Should().Be(context.AccountId.Value);
//             admin.EntityId.Should().Be(context.AccountId.Value);
//             context.EvaluateRelationShip(admin).Should().Be(AccessLevel.Owner);
//         }
//
//         [Fact]
//         public void Create_Organization()
//         {
//             var context = new OrganizationContext(Guid.NewGuid(), AccountIds.FCI);
//             context.CanCreate<User>().Should().BeTrue();
//             context.CanCreate<Organization>().Should().BeFalse();
//             context.CanCreate<Account>().Should().BeFalse();
//
//             var user = context.Create<User>();
//             user.AccountId.Should().Be(context.AccountId.Value);
//             user.EntityId.Should().Be(context.OrganizationId.Value);
//             context.EvaluateRelationShip(user).Should().Be(AccessLevel.Owner);
//
//             context.Invoking(x => x.Create<Organization>())
//                 .Should()
//                 .Throw<ForbiddenException>();
//         }
//
//         [Fact]
//         public void Create_Manager()
//         {
//             var context = UserContext.OrgUser(Guid.NewGuid(), "Manager", EntityRoleId.Manager, Guid.NewGuid(), AccountIds.FCI);
//             context.CanCreate<User>().Should().BeTrue();
//             context.CanCreate<Organization>().Should().BeFalse();
//             context.CanCreate<Account>().Should().BeFalse();
//
//             var user = context.Create<User>();
//             user.AccountId.Should().Be(context.AccountId.Value);
//             user.EntityId.Should().Be(context.OrganizationId.Value);
//             context.EvaluateRelationShip(user).Should().Be(AccessLevel.Owner);
//
//             context.Invoking(x => x.Create<Organization>())
//                 .Should()
//                 .Throw<ForbiddenException>();
//         }
//
//         [Fact]
//         public void Create_User()
//         {
//             var context = UserContext.OrgUser(Guid.NewGuid(), "User", EntityRoleId.User, Guid.NewGuid(), AccountIds.FCI);
//             context.CanCreate<User>().Should().BeFalse();
//             context.CanCreate<Organization>().Should().BeFalse();
//             context.CanCreate<Account>().Should().BeFalse();
//
//             context.Invoking(x => x.Create<User>())
//                 .Should()
//                 .Throw<ForbiddenException>();
//         }
//     }
// }