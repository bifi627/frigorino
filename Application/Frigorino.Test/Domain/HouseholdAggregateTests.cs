using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;

namespace Frigorino.Test.Domain
{
    // Pure unit tests for the Household aggregate. No DbContext, no DI — tests build aggregates
    // in-memory the same way EF would hydrate them, then call methods directly. Locks down the
    // invariant matrix (auth boundary, role policy, last-Owner protection, reactivation branch)
    // that the integration tests cover only on happy paths.
    public class HouseholdAggregateTests
    {
        private const string OwnerId = "user-owner";
        private const string AdminId = "user-admin";
        private const string MemberId = "user-member";
        private const string OutsiderId = "user-outsider";

        // ------- Create -------

        [Fact]
        public void Create_EmptyName_Fails()
        {
            var result = Household.Create("", "desc", OwnerId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(Household.Name));
        }

        [Fact]
        public void Create_EmptyOwnerId_Fails()
        {
            var result = Household.Create("Family", null, "");

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(Household.CreatedByUserId));
        }

        [Fact]
        public void Create_Valid_SeedsOwnerMembership()
        {
            var result = Household.Create("Family", "shared groceries", OwnerId);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value.IsActive);
            Assert.Single(result.Value.UserHouseholds);
            var owner = result.Value.UserHouseholds.Single();
            Assert.Equal(OwnerId, owner.UserId);
            Assert.Equal(HouseholdRole.Owner, owner.Role);
            Assert.True(owner.IsActive);
        }

        [Fact]
        public void Create_NameLongerThanMax_Fails()
        {
            var tooLong = new string('x', Household.NameMaxLength + 1);

            var result = Household.Create(tooLong, null, OwnerId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(Household.Name));
        }

        [Fact]
        public void Create_DescriptionLongerThanMax_Fails()
        {
            var tooLong = new string('x', Household.DescriptionMaxLength + 1);

            var result = Household.Create("Family", tooLong, OwnerId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(Household.Description));
        }

        [Fact]
        public void Create_AtMaxLengths_Succeeds()
        {
            var maxName = new string('x', Household.NameMaxLength);
            var maxDescription = new string('y', Household.DescriptionMaxLength);

            var result = Household.Create(maxName, maxDescription, OwnerId);

            Assert.True(result.IsSuccess);
        }

        // ------- AddMember -------

        [Fact]
        public void AddMember_CallerNotMember_ReturnsEntityNotFound()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner));

            var result = household.AddMember(OutsiderId, "new-user", HouseholdRole.Member);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void AddMember_CallerIsMember_ReturnsAccessDenied()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (MemberId, HouseholdRole.Member));

            var result = household.AddMember(MemberId, "new-user", HouseholdRole.Member);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void AddMember_AlreadyActive_ReturnsValidationKeyedOnEmail()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (MemberId, HouseholdRole.Member));

            var result = household.AddMember(OwnerId, MemberId, HouseholdRole.Member);

            Assert.True(result.IsFailed);
            var error = result.Errors[0];
            Assert.False(error is AccessDeniedError);
            Assert.False(error is EntityNotFoundError);
            Assert.Equal("email", error.Metadata["Property"]);
        }

        [Fact]
        public void AddMember_NewUser_AppendsMembership()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner));

            var result = household.AddMember(OwnerId, "new-user", HouseholdRole.Admin);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, household.UserHouseholds.Count);
            Assert.Contains(household.UserHouseholds, m => m.UserId == "new-user" && m.Role == HouseholdRole.Admin && m.IsActive);
        }

        [Fact]
        public void AddMember_InactiveExisting_ReactivatesAndUpdatesRole()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner));
            household.UserHouseholds.Add(new UserHousehold
            {
                UserId = "rejoiner",
                HouseholdId = household.Id,
                Role = HouseholdRole.Member,
                IsActive = false,
                JoinedAt = DateTime.UtcNow.AddYears(-1),
            });

            var result = household.AddMember(OwnerId, "rejoiner", HouseholdRole.Admin);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, household.UserHouseholds.Count);
            var rejoined = household.UserHouseholds.Single(m => m.UserId == "rejoiner");
            Assert.True(rejoined.IsActive);
            Assert.Equal(HouseholdRole.Admin, rejoined.Role);
            Assert.True(rejoined.JoinedAt > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public void AddMember_AdminGrantsOwner_ReturnsAccessDenied()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (AdminId, HouseholdRole.Admin));

            var result = household.AddMember(AdminId, "new-user", HouseholdRole.Owner);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void AddMember_OwnerGrantsOwner_Succeeds()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner));

            var result = household.AddMember(OwnerId, "new-co-owner", HouseholdRole.Owner);

            Assert.True(result.IsSuccess);
            Assert.Equal(HouseholdRole.Owner, household.UserHouseholds.Single(m => m.UserId == "new-co-owner").Role);
        }

        [Fact]
        public void AddMember_AdminReactivatesAtOwnerRole_ReturnsAccessDenied()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (AdminId, HouseholdRole.Admin));
            household.UserHouseholds.Add(new UserHousehold
            {
                UserId = "rejoiner",
                HouseholdId = household.Id,
                Role = HouseholdRole.Member,
                IsActive = false,
                JoinedAt = DateTime.UtcNow.AddYears(-1),
            });

            var result = household.AddMember(AdminId, "rejoiner", HouseholdRole.Owner);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
            // The inactive row stays inactive — no silent privilege flip.
            Assert.False(household.UserHouseholds.Single(m => m.UserId == "rejoiner").IsActive);
        }

        // ------- RemoveMember -------

        [Fact]
        public void RemoveMember_CallerNotMember_ReturnsEntityNotFound()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner));

            var result = household.RemoveMember(OutsiderId, OwnerId);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RemoveMember_TargetNotMember_ReturnsEntityNotFound()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner));

            var result = household.RemoveMember(OwnerId, "ghost");

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RemoveMember_MemberRemovesSelf_Succeeds()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (MemberId, HouseholdRole.Member));

            var result = household.RemoveMember(MemberId, MemberId);

            Assert.True(result.IsSuccess);
            Assert.False(household.UserHouseholds.Single(m => m.UserId == MemberId).IsActive);
        }

        [Fact]
        public void RemoveMember_MemberRemovesOther_ReturnsAccessDenied()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (MemberId, HouseholdRole.Member));

            var result = household.RemoveMember(MemberId, OwnerId);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void RemoveMember_OwnerRemovesAdmin_Succeeds()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (AdminId, HouseholdRole.Admin));

            var result = household.RemoveMember(OwnerId, AdminId);

            Assert.True(result.IsSuccess);
            Assert.False(household.UserHouseholds.Single(m => m.UserId == AdminId).IsActive);
        }

        [Fact]
        public void RemoveMember_LastOwnerRemovesSelf_ReturnsValidationKeyedOnUserId()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner));

            var result = household.RemoveMember(OwnerId, OwnerId);

            Assert.True(result.IsFailed);
            var error = result.Errors[0];
            Assert.False(error is AccessDeniedError);
            Assert.False(error is EntityNotFoundError);
            Assert.Equal("userId", error.Metadata["Property"]);
        }

        [Fact]
        public void RemoveMember_OwnerRemovesOwnerWithMultipleOwners_Succeeds()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), ("owner-2", HouseholdRole.Owner));

            var result = household.RemoveMember(OwnerId, "owner-2");

            Assert.True(result.IsSuccess);
        }

        // ------- ChangeMemberRole -------

        [Fact]
        public void ChangeMemberRole_CallerNotMember_ReturnsEntityNotFound()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (MemberId, HouseholdRole.Member));

            var result = household.ChangeMemberRole(OutsiderId, MemberId, HouseholdRole.Admin);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void ChangeMemberRole_CallerIsMember_ReturnsAccessDenied()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (MemberId, HouseholdRole.Member));

            var result = household.ChangeMemberRole(MemberId, OwnerId, HouseholdRole.Member);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void ChangeMemberRole_AdminTriesToChangeOwner_ReturnsAccessDenied()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (AdminId, HouseholdRole.Admin));

            var result = household.ChangeMemberRole(AdminId, OwnerId, HouseholdRole.Member);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void ChangeMemberRole_OwnerPromotesMemberToAdmin_Succeeds()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (MemberId, HouseholdRole.Member));

            var result = household.ChangeMemberRole(OwnerId, MemberId, HouseholdRole.Admin);

            Assert.True(result.IsSuccess);
            Assert.Equal(HouseholdRole.Admin, household.UserHouseholds.Single(m => m.UserId == MemberId).Role);
        }

        [Fact]
        public void ChangeMemberRole_LastOwnerSelfDemote_ReturnsValidationKeyedOnRole()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner));

            var result = household.ChangeMemberRole(OwnerId, OwnerId, HouseholdRole.Admin);

            Assert.True(result.IsFailed);
            var error = result.Errors[0];
            Assert.False(error is AccessDeniedError);
            Assert.False(error is EntityNotFoundError);
            Assert.Equal("role", error.Metadata["Property"]);
        }

        [Fact]
        public void ChangeMemberRole_OwnerSelfDemoteWithCoOwner_Succeeds()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), ("owner-2", HouseholdRole.Owner));

            var result = household.ChangeMemberRole(OwnerId, OwnerId, HouseholdRole.Admin);

            Assert.True(result.IsSuccess);
            Assert.Equal(HouseholdRole.Admin, household.UserHouseholds.Single(m => m.UserId == OwnerId).Role);
        }

        [Fact]
        public void ChangeMemberRole_AdminPromotesMemberToOwner_ReturnsAccessDenied()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (AdminId, HouseholdRole.Admin), (MemberId, HouseholdRole.Member));

            var result = household.ChangeMemberRole(AdminId, MemberId, HouseholdRole.Owner);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
            Assert.Equal(HouseholdRole.Member, household.UserHouseholds.Single(m => m.UserId == MemberId).Role);
        }

        [Fact]
        public void ChangeMemberRole_AdminSelfPromotesToOwner_ReturnsAccessDenied()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (AdminId, HouseholdRole.Admin));

            var result = household.ChangeMemberRole(AdminId, AdminId, HouseholdRole.Owner);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
            Assert.Equal(HouseholdRole.Admin, household.UserHouseholds.Single(m => m.UserId == AdminId).Role);
        }

        [Fact]
        public void ChangeMemberRole_OwnerPromotesMemberToOwner_Succeeds()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (MemberId, HouseholdRole.Member));

            var result = household.ChangeMemberRole(OwnerId, MemberId, HouseholdRole.Owner);

            Assert.True(result.IsSuccess);
            Assert.Equal(HouseholdRole.Owner, household.UserHouseholds.Single(m => m.UserId == MemberId).Role);
        }

        // ------- SoftDelete -------

        [Fact]
        public void SoftDelete_CallerNotMember_ReturnsEntityNotFound()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner));

            var result = household.SoftDelete(OutsiderId);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void SoftDelete_CallerIsMember_ReturnsAccessDenied()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (MemberId, HouseholdRole.Member));

            var result = household.SoftDelete(MemberId);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void SoftDelete_CallerIsAdmin_ReturnsAccessDenied()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (AdminId, HouseholdRole.Admin));

            var result = household.SoftDelete(AdminId);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void SoftDelete_CallerIsOwner_DeactivatesHouseholdAndAllMemberships()
        {
            var household = HouseholdWith((OwnerId, HouseholdRole.Owner), (AdminId, HouseholdRole.Admin), (MemberId, HouseholdRole.Member));
            var before = household.UpdatedAt;

            var result = household.SoftDelete(OwnerId);

            Assert.True(result.IsSuccess);
            Assert.False(household.IsActive);
            Assert.True(household.UpdatedAt > before);
            Assert.All(household.UserHouseholds, m => Assert.False(m.IsActive));
        }

        // ------- HouseholdRoleExtensions -------

        [Fact]
        public void CanManageMembers_TrueForAdminAndOwner_FalseForMember()
        {
            Assert.True(HouseholdRole.Owner.CanManageMembers());
            Assert.True(HouseholdRole.Admin.CanManageMembers());
            Assert.False(HouseholdRole.Member.CanManageMembers());
        }

        [Theory]
        // Owner can grant any role
        [InlineData(HouseholdRole.Owner, HouseholdRole.Owner, true)]
        [InlineData(HouseholdRole.Owner, HouseholdRole.Admin, true)]
        [InlineData(HouseholdRole.Owner, HouseholdRole.Member, true)]
        // Admin can grant Member or Admin, NOT Owner — privilege escalation gate
        [InlineData(HouseholdRole.Admin, HouseholdRole.Owner, false)]
        [InlineData(HouseholdRole.Admin, HouseholdRole.Admin, true)]
        [InlineData(HouseholdRole.Admin, HouseholdRole.Member, true)]
        // Member can't grant anything (also blocked by CanManageMembers upstream)
        [InlineData(HouseholdRole.Member, HouseholdRole.Owner, false)]
        [InlineData(HouseholdRole.Member, HouseholdRole.Admin, false)]
        [InlineData(HouseholdRole.Member, HouseholdRole.Member, true)]
        public void CanGrantRole_Matrix(HouseholdRole caller, HouseholdRole target, bool expected)
        {
            Assert.Equal(expected, caller.CanGrantRole(target));
        }

        // ------- Helpers -------

        private static Household HouseholdWith(params (string userId, HouseholdRole role)[] members)
        {
            var owner = members.FirstOrDefault(m => m.role == HouseholdRole.Owner);
            var ownerId = owner.userId ?? OwnerId;
            var household = new Household
            {
                Id = 1,
                Name = "Test",
                CreatedByUserId = ownerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1), // distinct from "now" so SoftDelete's stamp is detectable
                IsActive = true,
            };
            foreach (var (userId, role) in members)
            {
                household.UserHouseholds.Add(new UserHousehold
                {
                    UserId = userId,
                    HouseholdId = household.Id,
                    Role = role,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true,
                });
            }
            return household;
        }
    }
}
